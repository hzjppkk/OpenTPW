namespace OpenTPW;

/// <summary>The lobby response queue (0x5d1180), global speech and gesture playback.</summary>
public sealed class LobbyAdvisor
{
	// Response rows at 0x764a38: response ID, speech ID, lip ID, default gesture selector -1.
	private static readonly Dictionary<int, uint> Responses = new()
	{
		[0x186] = 465, [0x187] = 466, [0x18e] = 471, [0x18a] = 469,
		[0x23a] = 588, [0x250] = 611, [0x256] = 617
	};
	private readonly AudioSystem audio;
	private readonly Func<int> nextRandom;
	private readonly Queue<int> queue = new();
	private sealed record Prepared( uint Sample, int Duration, byte[] Lips );
	private readonly Dictionary<int, Prepared> prepared = new();
	private readonly ModelAnimation[] animations;
	private readonly int[] durations;
	private LobbyAdvisorSequence? sequence;
	private LobbyAdvisorLips? lips;
	private int segment;
	private int segmentStart;
	private double started;
	private long voice;
	private uint sample;
	private bool voiceStarted;
	public LobbyAdvisorGeometry Geometry { get; } = new();
	public bool Active => sequence != null;
	public int? Response { get; private set; }
	public int Mouth => lips?.Mouth ?? 1;
	public ModelAnimation Animation => animations[sequence!.Segments[segment]];
	public float Frame { get; private set; }
	public int PendingCount => queue.Count;

	public LobbyAdvisor( AudioSystem audio, Func<int>? nextRandom = null )
	{
		this.audio = audio;
		this.nextRandom = nextRandom ?? RetailRandom.Next;
		animations = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M ).Select( path => new ModelAnimation( path ) ).ToArray();
		if ( animations.Length != 15 ) throw new InvalidDataException( "Advisor requires fifteen M segments" );
		durations = animations.Select( animation => (int)(animation.Length * 1000 / ModelAnimation.FramesPerSecond) ).ToArray();
	}

	public void Welcome( bool hasPlayers )
	{
		Say( hasPlayers ? 0x18e : 0x186, true );
		if ( !hasPlayers ) Say( 0x187 );
	}
	public void Say( int response, bool replace = false )
	{
		if ( !Responses.ContainsKey( response ) ) throw new ArgumentOutOfRangeException( nameof(response), $"Unimplemented advisor response {response:X}" );
		if ( !prepared.ContainsKey( response ) )
		{
			uint id = Responses[response];
			var map = audio.Assets.Category( "global/speech", "cat_speech" );
			var sound = map.Sounds[id];
			if ( sound.Definitions.Count != 1 || sound.Definitions[0].Samples.Count != 1 )
				throw new InvalidDataException( "Advisor response requires a unique speech sample" );
			var entry = sound.Definitions[0].Samples[0];
			audio.Assets.Pcm( audio.Assets.Sample( "global/speech", map, entry ) );
			byte[] boundaries = FileSystem.ReadAllBytes( $"global/speech/lips/sp_{id:000}.lip" );
			_ = new LobbyAdvisorLips( boundaries );
			prepared.Add( response, new( id, checked((int)entry.DurationMilliseconds), boundaries ) );
		}
		if ( replace ) Cancel();
		queue.Enqueue( response );
	}
	public void Cancel()
	{
		queue.Clear();
		audio.Stop( voice );
		voice = 0;
		sequence = null;
		Response = null;
		lips = null;
	}

	public void Update( double seconds )
	{
		if ( sequence == null )
		{
			if ( !queue.TryDequeue( out int response ) ) return;
			// Resource loading takes place at enqueue time, before the playback clock starts.
			var entry = prepared[response];
			sample = entry.Sample;
			lips = new( entry.Lips );
			sequence = LobbyAdvisorSequence.Create( entry.Duration, durations, nextRandom );
			Response = response;
			started = seconds;
			segment = segmentStart = 0;
			voiceStarted = false;
		}
		int elapsed = Math.Max( 0, (int)((seconds - started) * 1000) );
		// 0x595500 starts at now-200, then offsets both speech and lip clock by 1000.
		if ( !voiceStarted && elapsed >= 800 )
		{
			voice = audio.Play( "global/speech", "cat_speech", sample );
			voiceStarted = true;
		}
		lips!.Update( elapsed - 800, nextRandom );
		// Animation completion advances one segment per game update (0x595e87).
		int duration = durations[sequence.Segments[segment]];
		if ( elapsed - segmentStart >= duration && segment + 1 < sequence.Segments.Length )
		{
			segment++;
			segmentStart = elapsed;
		}
		Frame = Animation.StartFrame + Math.Min( Animation.Length, (elapsed - segmentStart) * ModelAnimation.FramesPerSecond / 1000 );
		if ( elapsed >= sequence.DurationMilliseconds + 800 && !audio.IsPlaying( voice ) )
		{
			sequence = null;
			Response = null;
			lips = null;
		}
	}
}
