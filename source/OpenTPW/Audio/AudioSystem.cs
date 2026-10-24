using NAudio.Wave;

namespace OpenTPW;

/// <summary>Shared audio output for scene sounds, music and speech.</summary>
public sealed class AudioSystem : IDisposable
{
	public SoundMixer Mixer { get; } = new();
	public SoundAssets Assets { get; } = new();
	private readonly SoundSelection random;
	private sealed record Repeating( string Directory, string Category, SoundCategoryMap Map, SoundCategoryMap.Sound Sound,
		SoundCategoryMap.Definition Definition, SoundCategoryMap.Sample Sample, long Voice );
	private readonly Dictionary<long, Repeating> repeating = new();
	private readonly WaveOutEvent? output;
	private bool disposed;
	public bool Available { get; }
	public string? Failure { get; }
	private readonly float[] channelVolumes = new float[4];

	public AudioSystem( uint seed, bool openDevice = true )
	{
		random = new( seed );
		ConfigurePreferences( PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) ) );
		if ( !openDevice ) { Available = true; return; }
		try
		{
			output = new WaveOutEvent();
			output.Init( Mixer );
			output.Play();
			Available = true;
		}
		catch ( Exception error ) when ( error is NAudio.MmException or PlatformNotSupportedException or System.Runtime.InteropServices.COMException )
		{
			output?.Dispose();
			output = null;
			Failure = error.Message;
		}
	}

	public long Play( string directory, string category, uint id )
	{
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( !Available || id == 0 ) return 0;
		var map = Assets.Category( directory, category );
		if ( !map.Sounds.TryGetValue( id, out var sound ) ) throw new InvalidDataException( $"Sound {category}/{id} is not defined" );
		var definition = random.ChooseDefinition( sound );
		if ( definition == null ) return 0;
		var sample = random.ChooseSample( definition );
		if ( sample == null || sample.Bank == 0 ) return 0;
		var asset = Assets.Sample( directory, map, sample );
		// 0x6ba400 collapses a one-definition, one-sample repeat with no links/delay into a direct loop.
		bool directLoop = (sound.Flags & 8) != 0 || ((sound.Flags & 6) == 6 && sound.Definitions.Count == 1 &&
			definition.Samples.Count == 1 && definition.Relations.Count == 0 && definition.DelayMin == 0 && definition.DelayMax == 0);
		long handle = Mixer.Play( Assets.Pcm( asset ), random.Range( definition.VolumeMin, definition.VolumeMax ) / 100f * ChannelVolume( category ),
			directLoop, random.Range( definition.DelayMin, definition.DelayMax ) );
		if ( !directLoop && (sound.Flags & 4) != 0 ) repeating.Add( handle, new( directory, category, map, sound, definition, sample, handle ) );
		return handle;
	}

	public bool IsPlaying( long handle ) => repeating.ContainsKey( handle ) || Mixer.IsPlaying( handle );
	public void Stop( long handle )
	{
		if ( repeating.Remove( handle, out var entry ) ) Mixer.Stop( entry.Voice );
		else Mixer.Stop( handle );
	}
	public void Update()
	{
		foreach ( var (handle, entry) in repeating.ToArray() )
		{
			if ( Mixer.IsPlaying( entry.Voice ) ) continue;
			var definition = entry.Definition;
			// 0x6bdcd0..0x6bddd7: eligible links select using the destination definition weights.
			var links = definition.Relations.Where( link => ((link.Parameters >> 16) & 255) == 0 ).ToArray();
			if ( links.Length != 0 )
			{
				uint total = checked((uint)links.Sum( link => (long)entry.Sound.Definitions[(int)link.Definition - 1].Weight ));
				if ( total == 0 ) throw new InvalidDataException( "Repeating sound links have zero weight" );
				uint draw = random.Next() % total, cumulative = 0;
				foreach ( var link in links )
				{
					var candidate = entry.Sound.Definitions[(int)link.Definition - 1];
					cumulative += candidate.Weight;
					if ( cumulative < draw ) continue;
					definition = candidate;
					break;
				}
			}
			var sample = random.ChooseSample( definition );
			if ( sample == null || sample.Bank == 0 ) { repeating.Remove( handle ); continue; }
			// 0x6b67fc: continuation requests avoid an immediate repeat only with more than two candidates.
			if ( definition.Samples.Count > 2 && ReferenceEquals( sample, entry.Sample ) )
			{
				int index = definition.Samples.ToList().IndexOf( sample );
				sample = definition.Samples[(index + 1) % definition.Samples.Count];
			}
			long voice = Mixer.Play( Assets.Pcm( Assets.Sample( entry.Directory, entry.Map, sample ) ),
				random.Range( definition.VolumeMin, definition.VolumeMax ) / 100f * ChannelVolume( entry.Category ),
				delayMilliseconds: random.Range( definition.DelayMin, definition.DelayMax ) );
			repeating[handle] = entry with { Definition = definition, Sample = sample, Voice = voice };
		}
	}
	public void ConfigurePreferences( byte[] preferences )
	{
		if ( preferences.Length != 39 ) throw new InvalidDataException( "Invalid audio preferences" );
		for ( int i = 0; i < 4; i++ )
		{
			uint volume = BitConverter.ToUInt32( preferences, i * 8 + 4 );
			if ( volume > 100 ) throw new InvalidDataException( "Invalid audio channel volume" );
			channelVolumes[i] = preferences[i * 8] == 0 ? 0 : volume / 100f;
		}
	}
	private float ChannelVolume( string category ) => channelVolumes[category.EndsWith( "music", StringComparison.OrdinalIgnoreCase ) ? 1 :
		category.Equals( "cat_speech", StringComparison.OrdinalIgnoreCase ) ? 2 : 0];

	public long PlayFile( string path, float volume = 1 )
	{
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( !Available ) return 0;
		var file = new SoundFile( path );
		try { return Mixer.Play( Assets.Pcm( file.GetFile( new MemoryStream() ) ), volume ); }
		finally { file.Dispose(); }
	}
	public void Dispose()
	{
		if ( disposed ) return;
		disposed = true;
		repeating.Clear();
		output?.Stop();
		output?.Dispose();
		Mixer.StopAll();
	}
}
