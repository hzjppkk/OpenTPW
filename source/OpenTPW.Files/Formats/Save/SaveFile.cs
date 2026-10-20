namespace OpenTPW;

/// <summary>
/// The park in a save (.tpws) or level start file (.tpwi): the map and the things on it. Reads the
/// World module of the decompressed data (see <see cref="SaveReader"/>); the later modules are not read.
/// </summary>
public sealed class SaveFile : BaseFormat
{
	/*
	Decompressed data: modules in this order, each but the first and last followed by a four-character tag
	(a little-endian u32, so "WRLD" is stored as the bytes "DLRW"). Field names are the game's own.
		GActionRec:     4 bytes loaded published park flag, 4 bytes recording size, recording
		World:          'WRLD', below
		then Scripts 'SPSC', Particles 'PART', MsgCntr 'MESS', Clock 'CLOK', VanillaTime 'VANT', GameSystem 'GSYS',
		RideSystem 'RSYS', TrackRides 'TRAK', FlyingRides 'FLYR', 'RSSE', Camera 'KAME', Coasters 'COAS',
		Advisor 'ADVS', Sound 'SOUN', Cheat 'CHTS', AdvisorScoring 'ADSC', UI

	World
		64 bytes:   header (see ReadHeader)
		4806 bytes: object controls
		670 bytes:  staff pool
		46 bytes:   game time, visitor arrival
		128 x 128 map cells, row by row:
			1 byte:  parts present
			bit 0:   52 bytes - cell base, then litter, pylon and status fields
			bit 1:   31 bytes - cell base, 2 bytes track segment number
			bit 2:   10 bytes
		Things, a list linked by id:
			4 bytes: id of the first thing
			per thing: 4 bytes id of the next thing (0 ends the list), 4 bytes model, the model's data

	Cell base (29 bytes)
		1 byte direction, 2 bytes flags, 4 bytes mesh instance, 1 byte neighbours, 2 bytes overlap counter,
		2 bytes parent id, 12 bytes tile data, 4 bytes type, 1 byte hoarding neighbours

	Thing data starts with 2 bytes X, 2 bytes Y (256 per cell, so 128 is a cell's centre), 2 bytes map child,
	2 bytes map parent. Most models have a fixed size (ThingSizes); objects and the park analyser contain
	histories:
		4 bytes current entry, 4 bytes entry count, 1 byte wrapped around, temp value, entry count values
	*/

	public enum ThingModel
	{
		Visitor = 1,
		Object = 3,
		Mechanic = 4,
		Handyman = 5,
		Entertainer = 6,
		Guard = 7,
		Researcher = 8,
		StaffHQ = 9,
		MechanicHQ = 10,
		Advisor = 11,
		TagSystem = 12,
		ParkAnalyser = 13,
		ResearchLab = 14,
		Weather = 15,
		BankAccount = 16,
		UIMsgReceiver = 17,
		Challenges = 19
	}

	public class Thing
	{
		public int Id { get; set; }
		public ThingModel Model { get; set; }

		/// <summary>
		/// Position in 1/256 of a cell.
		/// </summary>
		public int X { get; set; }
		public int Y { get; set; }
	}

	/// <summary>
	/// A ride, shop, feature or other placed object.
	/// </summary>
	public class ParkObject : Thing
	{
		/// <summary>
		/// The object's Info.Id in its .sam file.
		/// </summary>
		public int TypeId { get; set; }

		/// <summary>
		/// 0, 90, 180 or 270.
		/// </summary>
		public int Angle { get; set; }
		public int Flags { get; set; }
		public int State { get; set; }

		/// <summary>
		/// Map cells of the top left corner and the entrance.
		/// </summary>
		public (int X, int Y) TopLeft { get; set; }
		public (int X, int Y) Entrance { get; set; }
	}

	public struct MapCell
	{
		/// <summary>
		/// Which of the three cell parts are present (bits 0 to 2).
		/// </summary>
		public byte Parts;

		/// <summary>
		/// Type from the first part, or -1 without it. 7 is outside the park.
		/// </summary>
		public int Type;
		public int Direction;
		public int Flags;
		public int ParentId;
	}

	public const int MapSize = 128;

	private static readonly Dictionary<ThingModel, int> ThingSizes = new()
	{
		[ThingModel.Visitor] = 525,
		[ThingModel.Mechanic] = 503,
		[ThingModel.Handyman] = 505,
		[ThingModel.Entertainer] = 501,
		[ThingModel.Guard] = 503,
		[ThingModel.Researcher] = 501,
		[ThingModel.StaffHQ] = 95,
		[ThingModel.MechanicHQ] = 10,
		[ThingModel.Advisor] = 5838,
		[ThingModel.TagSystem] = 8,
		[ThingModel.ResearchLab] = 4182,
		[ThingModel.Weather] = 91,
		[ThingModel.BankAccount] = 292,
		[ThingModel.UIMsgReceiver] = 8,
		[ThingModel.Challenges] = 929
	};

	public int WorldVersion { get; private set; }
	public uint GameTick { get; private set; }
	public int VisitorsToDate { get; private set; }
	public bool ParkClosed { get; private set; }

	/// <summary>
	/// From the bank account thing.
	/// </summary>
	public int Balance { get; private set; }
	public int AdmissionFee { get; private set; }

	/// <summary>
	/// <see cref="MapSize"/> x <see cref="MapSize"/>, row by row.
	/// </summary>
	public MapCell[] Map { get; private set; }
	public List<Thing> Things { get; } = new();
	public IEnumerable<ParkObject> Objects => Things.OfType<ParkObject>();

	public SaveFile( string path )
	{
		ReadFromFile( path );
	}

	public SaveFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( new MemoryStream( new SaveReader( stream ).ReadFile() ) );

		_ = reader.ReadUInt32();
		reader.Skip( reader.ReadInt32() );

		ReadHeader( reader );
		reader.Skip( 4806 + 670 + 46 );
		ReadMap( reader );
		ReadThings( reader );

		var tag = reader.ReadUInt32();
		if ( tag != 0x57524C44 )
			throw new Exception( $"World module did not end with its tag: {tag:X8}" );
	}

	private void ReadHeader( BinaryReader reader )
	{
		WorldVersion = reader.ReadInt32();
		reader.Skip( 2 * 5 );                 // arrival vehicle sizes, bank account and current arrival vehicle ids
		GameTick = reader.ReadUInt32();
		reader.Skip( 2 * 2 );                 // mechanic HQ and park analyser ids
		ParkClosed = reader.ReadUInt32() != 0;
		VisitorsToDate = reader.ReadInt32();
		reader.Skip( 2 * 2 + 4 + 2 * 5 + 4 + 2 * 6 ); // gate and light ids, random seed, singleton ids, world state, first staff ids
	}

	private void ReadMap( BinaryReader reader )
	{
		Map = new MapCell[MapSize * MapSize];
		for ( int i = 0; i < Map.Length; i++ )
		{
			var cell = new MapCell { Parts = reader.ReadByte(), Type = -1 };
			if ( (cell.Parts & 1) != 0 )
			{
				cell.Direction = reader.ReadByte();
				cell.Flags = reader.ReadUInt16();
				reader.Skip( 4 + 1 + 2 );         // mesh instance, neighbours, overlap counter
				cell.ParentId = reader.ReadUInt16();
				reader.Skip( 12 );
				cell.Type = reader.ReadInt32();
				reader.Skip( 1 + 52 - 29 );
			}

			if ( (cell.Parts & 2) != 0 )
				reader.Skip( 31 );

			if ( (cell.Parts & 4) != 0 )
				reader.Skip( 10 );

			Map[i] = cell;
		}
	}

	private void ReadThings( BinaryReader reader )
	{
		var id = reader.ReadInt32();
		while ( id != 0 )
		{
			var next = reader.ReadInt32();
			var model = (ThingModel)reader.ReadInt32();
			var start = reader.BaseStream.Position;

			var thing = model == ThingModel.Object ? new ParkObject() : new Thing();
			thing.Id = id;
			thing.Model = model;
			thing.X = reader.ReadUInt16();
			thing.Y = reader.ReadUInt16();
			reader.Skip( 4 );                 // map child and parent

			if ( thing is ParkObject parkObject )
				ReadObject( reader, parkObject );
			else if ( model == ThingModel.ParkAnalyser )
				SkipParkAnalyser( reader );
			else if ( ThingSizes.TryGetValue( model, out var size ) )
			{
				if ( model == ThingModel.BankAccount )
				{
					AdmissionFee = reader.ReadInt32();
					Balance = reader.ReadInt32();
				}

				reader.BaseStream.Position = start + size;
			}
			else
			{
				throw new Exception( $"Unknown thing model {(int)model}" );
			}

			Things.Add( thing );
			id = next;
		}
	}

	private static (int X, int Y) Cell( int index ) => (index % MapSize, index / MapSize);

	private static void ReadObject( BinaryReader reader, ParkObject parkObject )
	{
		parkObject.Angle = reader.ReadInt32();
		parkObject.TypeId = reader.ReadUInt16();
		reader.Skip( 8 * 4 + 4 );             // tv_t, mesh instance
		parkObject.Flags = reader.ReadUInt16();
		reader.Skip( 33 * 4 + 4 + 4 );        // names, ride script and track ride handles
		parkObject.State = reader.ReadInt32();
		parkObject.TopLeft = Cell( reader.ReadUInt16() );
		parkObject.Entrance = Cell( reader.ReadUInt16() );
		reader.Skip( 2 * 3 + 4 + 2 + 2 + 4 + 2 ); // next, assigned staff, back of queue, can load, exit, first in queue...

		SkipHistory( reader, 4, 4 );
		SkipHistory( reader, 4, 4 );
		reader.Skip( 4 );                     // customer count
		SkipHistory( reader, 4, 4 );
		reader.Skip( 4 );                     // walk away count
		for ( int i = 0; i < 3; i++ )
			SkipHistory( reader, 4, 4 );

		reader.Skip( 1 + 1 + 4 + 2 + 6 * 4 + 3 * 4 + 5 * 4 + 1 ); // operating settings, prices, costs, upgrade level
	}

	private static void SkipParkAnalyser( BinaryReader reader )
	{
		reader.Skip( 5 * 4 + 0x400 * (4 * 4 + 9 * 5) );
		for ( int i = 0; i < 4; i++ )
			SkipHistory( reader, 2, 2 );

		reader.Skip( 6 * 5 );
		for ( int i = 0; i < 18; i++ )
			SkipHistory( reader, 4, 4 );

		for ( int i = 0; i < 20; i++ )
			SkipHistory( reader, 1, 1 );

		reader.Skip( 4 * 4 + 6 * 8 + 3 * 4 );  // misbehaving kids, lifetime visitors, times of last events, records
	}

	private static void SkipHistory( BinaryReader reader, int tempSize, int entrySize )
	{
		reader.Skip( 4 );
		var count = reader.ReadInt32();
		reader.Skip( 1 + tempSize + count * entrySize );
	}
}

file static class BinaryReaderSkip
{
	public static void Skip( this BinaryReader reader, int count ) => reader.BaseStream.Position += count;
}
