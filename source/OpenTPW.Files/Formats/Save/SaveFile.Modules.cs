namespace OpenTPW;

public sealed partial class SaveFile
{
	/*
	The modules after World, in file order (loaders are TP.exe addresses). Every one but GActionRec and UI ends with
	its tag, which is checked. Field names are the game's own where it has them.

	Scripts (0x4739F0)
		4 bytes 'SCPT' (stored "TPCS"), 4 bytes 0x118, 4 bytes slot count, slot count x 4 bytes pointers (only
		zero / non-zero matters), then 0x118 bytes for each non-zero slot except slot 0.
	Particles (0x51BEC0)
		4 bytes 'PTCL' (stored "LCTP"), 4 bytes saved flag; if set: 4 bytes 0x9E68 and that many bytes, 4 bytes 0x8B60 and
		that many bytes, 4 bytes size and that many bytes.
	MsgCntr (0x40FCF0)
		4 bytes, then 29 queues: 4 bytes count, count x 2 bytes message ids.
	Clock (0x402DC0)
		4 bytes at +0x4C, 4 bytes at +0x24.
	VanillaTime (0x4031F0)
		4 bytes, relative to the machine's timer.
	GameSystem (0x54CCB0)
		9 x 4 bytes.
	RideSystem (0x4633A0)
		4 bytes size, then 3 x 4 bytes: ride slots in use, in use (second list), third count. Then one slot per ride,
		a + b of them: 1 byte present, and for present rides 0x36 bytes (see ReadRide), 2 x 4 bytes per embedded node,
		4 bytes per node flag word, then one 44-byte anim time control per control the ride's model has.
		The number of anim time controls is not in the file; it is found by trying the counts that make every record
		line up with the end of the module (inferred).
	TrackRides (0x53FB90)
		A chunk is 4 bytes kind, 4 bytes limit, 4 bytes size. Chunk 1 holds the sub chunks (2: a 12-byte build
		stamp text, 3: track ride state, 4..9: peeps, cars and positions), each as long as its size says.
	FlyingRides (0x55A650)
		4 bytes 'FLY_', 4 bytes saved flag; if set: 4 bytes size, a table of u32 (100 slots), 4 x 4 bytes, 6 x 4 bytes
		with the slot size last, then a block of that size for every non-zero slot after the first.
	RSSE (0x555DF0)
		4 bytes 'RSSE', 4 bytes size, block; 5 x 4 bytes, 4 bytes instance count, 4 bytes instance size. Per instance:
		the record, then blocks of 4 bytes size and data: code, 2 tables, 1 table, 1 table, 1 table; a block of
		size x 32 bytes; 2 more blocks; 4 bytes 'OBJ ', 4 bytes count, 4 bytes object size, count x object.
	Camera (0x42C830)
		gf_CameraZoom f32, gf_YRotation f32, gui_CameraFlags u32, gs_RequiredPOIPosition 3 x f32,
		gs_SavedPOIPosition 3 x f32, gf_SavedYRotation f32.
	Coasters (0x436EB0)
		4 bytes coaster count, 3 x 4 bytes globals. Per coaster: 0x20 bytes header (u32 flags, ..., u16 thing at +10, u16 piece count at +0x16), 4 bytes count and
		that many u16, per piece 0x22 bytes and a u16 list (length in its last byte), then per train 0x1B bytes and per car 11 bytes followed by
		two lists of 4-byte values, as long as the car's bytes 4 and 5 say. The train and car counts
		come from the coaster's model and no sample has a coaster (inferred, untested).
	Advisor (0x5961B0)
		4 bytes 'ADV_', 4 bytes size and block, 4 bytes size and block (the message queue, usually empty).
	Sound (0x518BB0)
		4 bytes enabled, 4 bytes flag, 4 bytes size and block. If both flags are set, the rest is written by a
		function not in the decompiled set (inferred from the data): 3 x f32, 3 x u32, 4 bytes bank count and that
		many u32, then each bank as a length byte and text, 4 bytes, then the sound objects. Each is guarded by the
		value 0x12D591 three times (a 36-byte body, then a list of 24-byte entries, then 1 + 4 bytes) and may be
		followed by a nested object.
	Cheat (0x4055A0)
		2 bytes.
	AdvisorScoring (0x598D00)
		4 bytes.
	UI (0x47F960)
		4 bytes count, count x 0x21C bytes.
	*/

	private const uint SoundGuard = 0x12D591;

	public class Ride
	{
		/// <summary>
		/// The object's Info.Id in its .sam file.
		/// </summary>
		public int TypeId { get; set; }

		/// <summary>
		/// Map cell of the corner and size in cells.
		/// </summary>
		public (int X, int Y) Position { get; set; }
		public (int Width, int Height) Size { get; set; }
		public int Kind { get; set; }

		/// <summary>
		/// Handle in the game's table of objects, counting from 1; 0 for the models that are not placed objects.
		/// </summary>
		public int Handle { get; set; }
		public uint Flags { get; set; }
		public int Angle { get; set; }
		public int AnimTimeControls { get; set; }
	}

	public class Coaster
	{
		public uint Flags { get; set; }
		public int Thing { get; set; }
		public int Pieces { get; set; }
		public int Trains { get; set; }
		public int CarsPerTrain { get; set; }
	}

	/// <summary>
	/// 'Clock' module: the value at +0x4C and the one at +0x24, in milliseconds. Close to the game's
	/// GameSystem times; their roles are not confirmed.
	/// </summary>
	public uint ClockTime { get; private set; }
	public uint ClockStart { get; private set; }

	/// <summary>
	/// Milliseconds of VanillaTime, relative to the machine's timer when it was saved.
	/// </summary>
	public uint VanillaTime { get; private set; }

	/// <summary>
	/// The nine values of the GameSystem module; the first three are times near <see cref="ClockTime"/>.
	/// </summary>
	public uint[] GameSystem { get; private set; }

	/// <summary>
	/// One entry per ride system slot in use: the placed objects (handle 1 and up, Kind 815 or 865) and models the
	/// ride system animates without a handle (Kind 826; in the samples they stand along x = 69, outside the park).
	/// </summary>
	public List<Ride> Rides { get; } = new();
	public IEnumerable<Ride> PlacedRides => Rides.Where( x => x.Handle != 0 );

	/// <summary>
	/// Build time stamp of the game that wrote the save, from the track rides module, e.g. "10:12:51Nov 17 1999".
	/// </summary>
	public string BuildStamp { get; private set; }

	/// <summary>
	/// Number of flying rides in the save.
	/// </summary>
	public int FlyingRides { get; private set; }
	public int ScriptCount { get; private set; }
	public int MessageCount { get; private set; }
	public int RideScriptInstances { get; private set; }
	public List<Coaster> Coasters { get; } = new();

	public float CameraZoom { get; private set; }
	public float CameraYRotation { get; private set; }
	public uint CameraFlags { get; private set; }
	/// <summary>
	/// gs_RequiredPOIPosition.
	/// </summary>
	public (float X, float Y, float Z) CameraPointOfInterest { get; private set; }

	/// <summary>
	/// Sound banks the level had loaded, e.g. "\data\global\sound\cat_ambient".
	/// </summary>
	public List<string> SoundBanks { get; } = new();
	public int SoundObjects { get; private set; }
	public uint AdvisorScoring { get; private set; }
	public bool CheatsUsed => CheatFlags != 0;
	public int CheatFlags { get; private set; }

	/// <summary>
	/// Start and size of every module in the decompressed data, by name, as checked while reading.
	/// </summary>
	public Dictionary<string, (long Start, long Size)> ModuleRanges { get; } = new();

	private static uint Tag( string name ) => (uint)(name[3] | name[2] << 8 | name[1] << 16 | name[0] << 24);
	private static uint Magic( string name ) => (uint)(name[0] | name[1] << 8 | name[2] << 16 | name[3] << 24);

	private static void ExpectTag( BinaryReader reader, string name )
	{
		var tag = reader.ReadUInt32();
		if ( tag != Tag( name ) )
			throw new Exception( $"{name} module did not end with its tag: {tag:X8}" );
	}

	private static void ExpectMagic( BinaryReader reader, string name )
	{
		var magic = reader.ReadUInt32();
		if ( magic != Magic( name ) )
			throw new Exception( $"Bad header of the {name} block: {magic:X8}" );
	}

	private void Module( BinaryReader reader, string name, long start, string tag )
	{
		ModuleRanges[name] = (start, reader.BaseStream.Position - start);
		if ( tag != null )
			ExpectTag( reader, tag );
	}

	private void ReadModules( BinaryReader reader )
	{
		var data = ((MemoryStream)reader.BaseStream).ToArray();
		long start;

		start = reader.BaseStream.Position;
		ReadScripts( reader );
		Module( reader, "Scripts", start, "SPSC" );

		start = reader.BaseStream.Position;
		ReadParticles( reader );
		Module( reader, "Particles", start, "PART" );

		start = reader.BaseStream.Position;
		ReadMessages( reader );
		Module( reader, "MsgCntr", start, "MESS" );

		start = reader.BaseStream.Position;
		ClockTime = reader.ReadUInt32();
		ClockStart = reader.ReadUInt32();
		Module( reader, "Clock", start, "CLOK" );

		start = reader.BaseStream.Position;
		VanillaTime = reader.ReadUInt32();
		Module( reader, "VanillaTime", start, "VANT" );

		start = reader.BaseStream.Position;
		GameSystem = new uint[9];
		for ( int i = 0; i < GameSystem.Length; i++ )
			GameSystem[i] = reader.ReadUInt32();
		Module( reader, "GameSystem", start, "GSYS" );

		start = reader.BaseStream.Position;
		ReadRideSystem( reader );
		Module( reader, "RideSystem", start, "RSYS" );

		start = reader.BaseStream.Position;
		ReadTrackRides( reader );
		Module( reader, "TrackRides", start, "TRAK" );

		start = reader.BaseStream.Position;
		ReadFlyingRides( reader );
		Module( reader, "FlyingRides", start, "FLYR" );

		start = reader.BaseStream.Position;
		ReadRsse( reader );
		Module( reader, "RSSE", start, "RSSE" );

		start = reader.BaseStream.Position;
		ReadCamera( reader );
		Module( reader, "Camera", start, "KAME" );

		start = reader.BaseStream.Position;
		ReadCoasters( reader, data );
		Module( reader, "Coasters", start, "COAS" );

		start = reader.BaseStream.Position;
		ExpectMagic( reader, "ADV_" );
		reader.Skip( reader.ReadInt32() );
		reader.Skip( reader.ReadInt32() );
		Module( reader, "Advisor", start, "ADVS" );

		start = reader.BaseStream.Position;
		ReadSound( reader );
		Module( reader, "Sound", start, "SOUN" );

		start = reader.BaseStream.Position;
		CheatFlags = reader.ReadByte() | reader.ReadByte() << 8;
		Module( reader, "Cheat", start, "CHTS" );

		start = reader.BaseStream.Position;
		AdvisorScoring = reader.ReadUInt32();
		Module( reader, "AdvisorScoring", start, "ADSC" );

		start = reader.BaseStream.Position;
		reader.Skip( reader.ReadInt32() * 0x21C );
		Module( reader, "UI", start, null );

		if ( reader.BaseStream.Position != reader.BaseStream.Length )
			throw new Exception( $"{reader.BaseStream.Length - reader.BaseStream.Position} bytes left after the UI module" );
	}

	private void ReadScripts( BinaryReader reader )
	{
		ExpectMagic( reader, "TPCS" );
		if ( reader.ReadUInt32() != 0x118 )
			throw new Exception( "Scripts module has an unexpected script size" );

		var slots = reader.ReadInt32();
		var used = new bool[slots];
		for ( int i = 0; i < slots; i++ )
			used[i] = reader.ReadUInt32() != 0;

		for ( int i = 1; i < slots; i++ )
		{
			if ( !used[i] )
				continue;

			reader.Skip( 0x118 );
			ScriptCount++;
		}
	}

	private static void ReadParticles( BinaryReader reader )
	{
		ExpectMagic( reader, "LCTP" );
		if ( reader.ReadUInt32() == 0 )
			return;

		for ( int i = 0; i < 3; i++ )
			reader.Skip( reader.ReadInt32() );
	}

	private void ReadMessages( BinaryReader reader )
	{
		reader.Skip( 4 );
		for ( int i = 0; i < 29; i++ )
		{
			var count = reader.ReadInt32();
			reader.Skip( 2 * count );
			MessageCount += count;
		}
	}

	private void ReadRideSystem( BinaryReader reader )
	{
		var size = reader.ReadInt32();
		var blob = reader.ReadBytes( size );
		var slots = (int)(BitConverter.ToUInt32( blob, 0 ) + BitConverter.ToUInt32( blob, 4 ));

		var found = new List<(int Offset, int Controls)>();
		if ( !FindRides( blob, 12, 0, slots, found ) )
			throw new Exception( "Ride system records do not line up" );

		foreach ( var (offset, controls) in found )
		{
			Rides.Add( new Ride
			{
				TypeId = BitConverter.ToInt32( blob, offset + 1 ),
				Position = (BitConverter.ToInt32( blob, offset + 5 ), BitConverter.ToInt32( blob, offset + 9 )),
				Size = (BitConverter.ToInt32( blob, offset + 13 ), BitConverter.ToInt32( blob, offset + 17 )),
				Kind = BitConverter.ToInt32( blob, offset + 21 ),
				Handle = BitConverter.ToInt32( blob, offset + 25 ),
				Flags = BitConverter.ToUInt32( blob, offset + 29 ),
				Angle = BitConverter.ToInt32( blob, offset + 0x23 ),
				AnimTimeControls = controls
			} );
		}
	}

	private static bool AnimControlFits( byte[] blob, int offset )
	{
		return offset + 44 <= blob.Length
			&& BitConverter.ToUInt32( blob, offset + 4 ) <= 12
			&& BitConverter.ToUInt32( blob, offset + 28 ) <= 12;
	}

	/// <summary>
	/// A ride's record is 0x37 bytes, 8 per embedded node and 4 per node, then its anim time controls. Their count
	/// belongs to the ride's model, so try the counts until the records end exactly at the end of the module.
	/// </summary>
	private static bool FindRides( byte[] blob, int offset, int index, int slots, List<(int, int)> found )
	{
		if ( index == slots )
			return offset == blob.Length;

		if ( offset >= blob.Length )
			return false;

		if ( blob[offset] == 0 )
			return FindRides( blob, offset + 1, index + 1, slots, found );

		if ( blob[offset] != 1 )
			return false;

		var nodes = BitConverter.ToUInt16( blob, offset + 0x2B );
		var embedded = BitConverter.ToUInt16( blob, offset + 0x2D );
		var next = offset + 0x37 + embedded * 8 + nodes * 4;

		for ( int controls = 0; ; controls++ )
		{
			found.Add( (offset, controls) );
			if ( FindRides( blob, next, index + 1, slots, found ) )
				return true;

			found.RemoveAt( found.Count - 1 );
			if ( !AnimControlFits( blob, next ) )
				return false;

			next += 44;
		}
	}

	private void ReadTrackRides( BinaryReader reader )
	{
		var start = reader.BaseStream.Position;
		var kind = reader.ReadUInt32();
		reader.Skip( 4 );
		var size = reader.ReadInt32();
		if ( kind != 1 )
			throw new Exception( $"Unexpected track rides chunk {kind}" );

		var end = start + size;
		while ( reader.BaseStream.Position < end )
		{
			var chunkStart = reader.BaseStream.Position;
			var chunkKind = reader.ReadUInt32();
			var limit = reader.ReadInt32();
			var chunkSize = reader.ReadInt32();
			if ( chunkKind == 2 )
				BuildStamp = System.Text.Encoding.Latin1.GetString( reader.ReadBytes( Math.Min( limit, chunkSize ) - 12 ) ).TrimEnd( '\0' );

			reader.BaseStream.Position = chunkStart + chunkSize;
		}

		if ( reader.BaseStream.Position != end )
			throw new Exception( "Track rides chunk overran" );
	}

	private void ReadFlyingRides( BinaryReader reader )
	{
		ExpectMagic( reader, "FLY_" );
		if ( reader.ReadUInt32() == 0 )
			return;

		var tableSize = reader.ReadInt32();
		var used = new bool[tableSize / 4];
		for ( int i = 0; i < used.Length; i++ )
			used[i] = reader.ReadUInt32() != 0;

		reader.Skip( 4 * 4 + 5 * 4 );
		var slotSize = reader.ReadInt32();
		for ( int i = 1; i < 100 && i < used.Length; i++ )
		{
			if ( !used[i] )
				continue;

			reader.Skip( slotSize );
			FlyingRides++;
		}
	}

	private void ReadRsse( BinaryReader reader )
	{
		ExpectMagic( reader, "RSSE" );
		reader.Skip( reader.ReadInt32() );
		reader.Skip( 5 * 4 );
		var count = reader.ReadInt32();
		var recordSize = reader.ReadInt32();

		for ( int i = 0; i < count; i++ )
		{
			reader.Skip( recordSize );
			for ( int block = 0; block < 6; block++ )
				reader.Skip( reader.ReadInt32() );

			reader.Skip( reader.ReadInt32() * 32 );
			for ( int block = 0; block < 2; block++ )
				reader.Skip( reader.ReadInt32() );

			ExpectMagic( reader, "OBJ " );
			var objects = reader.ReadInt32();
			reader.Skip( objects * reader.ReadInt32() );
		}

		RideScriptInstances = count;
	}

	private void ReadCamera( BinaryReader reader )
	{
		CameraZoom = reader.ReadSingle();
		CameraYRotation = reader.ReadSingle();
		CameraFlags = reader.ReadUInt32();
		CameraPointOfInterest = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		reader.Skip( 3 * 4 + 4 );             // gs_SavedPOIPosition, gf_SavedYRotation
	}

	private void ReadCoasters( BinaryReader reader, byte[] data )
	{
		var count = reader.ReadInt32();
		reader.Skip( 3 * 4 );

		var end = Array.IndexOf( data, (byte)'S', (int)reader.BaseStream.Position );
		while ( end >= 0 && BitConverter.ToUInt32( data, end ) != Tag( "COAS" ) )
			end = Array.IndexOf( data, (byte)'S', end + 1 );

		for ( int i = 0; i < count; i++ )
		{
			var coaster = new Coaster();
			var header = reader.ReadBytes( 0x20 );
			coaster.Flags = BitConverter.ToUInt32( header, 0 );
			coaster.Thing = BitConverter.ToUInt16( header, 10 );
			coaster.Pieces = BitConverter.ToUInt16( header, 0x16 );
			reader.Skip( 2 * reader.ReadInt32() );

			for ( int piece = 0; piece < coaster.Pieces; piece++ )
			{
				var body = reader.ReadBytes( 0x22 );
				reader.Skip( 2 * body[0x21] );
			}

			// The number of trains and cars is the coaster's model's, so try the counts until the data lines up.
			var position = (int)reader.BaseStream.Position;
			var last = i == count - 1;
			var done = false;
			for ( int trains = 0; trains <= 16 && !done; trains++ )
			{
				for ( int cars = 0; cars <= (trains == 0 ? 0 : 16) && !done; cars++ )
				{
					var offset = position;
					var fits = true;
					for ( int train = 0; train < trains && fits; train++ )
					{
						offset += 0x1B;
						for ( int car = 0; car < cars; car++ )
						{
							if ( offset + 11 > end )
							{
								fits = false;
								break;
							}

							offset += 11 + 4 * (data[offset + 4] + data[offset + 5]);
						}

						fits &= offset <= end;
					}

					if ( !fits || (last ? offset != end : offset + 0x20 > end) )
						continue;

					coaster.Trains = trains;
					coaster.CarsPerTrain = cars;
					reader.BaseStream.Position = offset;
					done = true;
				}
			}

			if ( !done )
				throw new Exception( "Coaster trains do not line up" );

			Coasters.Add( coaster );
		}
	}

	private void ReadSound( BinaryReader reader )
	{
		var enabled = reader.ReadUInt32();
		var flag = reader.ReadUInt32();
		reader.Skip( reader.ReadInt32() );
		if ( enabled == 0 || flag == 0 )
			return;

		reader.Skip( 3 * 4 + 4 );
		var objects = reader.ReadInt32();
		reader.Skip( 4 );
		var banks = reader.ReadInt32();
		reader.Skip( 4 * banks );
		for ( int i = 0; i < banks; i++ )
		{
			var name = System.Text.Encoding.Latin1.GetString( reader.ReadBytes( reader.ReadByte() ) ).TrimEnd( '\0' );
			if ( name.Length > 0 )
				SoundBanks.Add( name );
		}

		reader.Skip( 4 );
		for ( int i = 0; i < objects; i++ )
		{
			reader.Skip( 4 );                 // handle
			ReadSoundObject( reader );
			SoundObjects++;
		}
	}

	private static void ExpectSoundGuard( BinaryReader reader )
	{
		if ( reader.ReadUInt32() != SoundGuard )
			throw new Exception( $"Sound object guard missing at {reader.BaseStream.Position - 4:X}" );
	}

	private static void ReadSoundObject( BinaryReader reader )
	{
		reader.Skip( 4 );                     // kind
		ExpectSoundGuard( reader );
		reader.Skip( 36 );
		ExpectSoundGuard( reader );
		reader.Skip( 24 * reader.ReadInt32() );
		ExpectSoundGuard( reader );
		reader.Skip( 1 + 4 );
		if ( reader.ReadUInt32() != 0 )
			ReadSoundObject( reader );
	}
}

file static class BinaryReaderSkip
{
	public static void Skip( this BinaryReader reader, int count ) => reader.BaseStream.Position += count;
}
