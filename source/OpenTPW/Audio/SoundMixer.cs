using NAudio.Wave;

namespace OpenTPW;

/// <summary>One output clock with independently stoppable voices. No per-sound-ID suppression.</summary>
public sealed class SoundMixer : ISampleProvider
{
	private sealed class Voice
	{
		internal float[] Samples = [];
		internal int Position;
		internal float Volume;
		internal bool Loop;
		internal long Delay;
		internal uint Priority;
	}
	private readonly object sync = new();
	private readonly Dictionary<long, Voice> voices = new();
	private long nextHandle;
	private int voiceLimit = 30;
	public int VoiceLimit
	{
		get { lock ( sync ) return voiceLimit; }
		set { if ( value < 1 || value > 30 ) throw new ArgumentOutOfRangeException( nameof(value) ); lock ( sync ) voiceLimit = value; }
	}
	public WaveFormat WaveFormat { get; }
	public int ActiveVoices { get { lock ( sync ) return voices.Count; } }

	public SoundMixer( int sampleRate = 44100, int channels = 2 )
	{
		WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat( sampleRate, channels );
	}
	public long Play( float[] samples, float volume = 1, bool loop = false, int delayMilliseconds = 0, uint priority = 0xffff )
	{
		if ( samples.Length == 0 || samples.Length % WaveFormat.Channels != 0 ) throw new ArgumentException( "Audio must contain complete frames", nameof(samples) );
		if ( !float.IsFinite( volume ) || volume < 0 || volume > 1 || delayMilliseconds < 0 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		lock ( sync )
		{
			long handle = checked(++nextHandle);
			voices.Add( handle, new Voice { Samples = samples, Volume = volume, Loop = loop,
				Delay = (long)delayMilliseconds * WaveFormat.SampleRate / 1000 * WaveFormat.Channels, Priority = priority } );
			return handle;
		}
	}
	public bool IsPlaying( long handle ) { lock ( sync ) return voices.ContainsKey( handle ); }
	public void SetVolume( long handle, float volume )
	{
		if ( !float.IsFinite( volume ) || volume < 0 || volume > 1 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		lock ( sync ) if ( voices.TryGetValue( handle, out var voice ) ) voice.Volume = volume;
	}
	public void Stop( long handle ) { lock ( sync ) voices.Remove( handle ); }
	public void StopAll() { lock ( sync ) voices.Clear(); }
	public double PositionSeconds( long handle )
	{
		lock ( sync ) return voices.TryGetValue( handle, out var voice ) ? (double)voice.Position / WaveFormat.SampleRate / WaveFormat.Channels : 0;
	}
	public int Read( float[] buffer, int offset, int count )
	{
		if ( count % WaveFormat.Channels != 0 ) throw new ArgumentException( "Audio read must contain complete frames", nameof(count) );
		// NAudio's WaveBuffer can expose a byte[] through its float[] overlay.
		// Array.Clear uses the runtime array element size and would clear only count bytes.
		// Typed stores clear count float samples, including when the backing array is bytes.
		for ( int i = offset; i < offset + count; ++i ) buffer[i] = 0;
		lock ( sync )
		{
			// 0x6b1260/0x6b1be0 rank output candidates without destroying logical instances.
			// 0x6b1a50 inserts new instances at the head, so newer equal-priority instances are visited first.
			var ordered = voices.OrderByDescending( entry => entry.Value.Priority ).ThenByDescending( entry => entry.Key ).ToArray();
			for ( int i = 0; i < count; i += WaveFormat.Channels )
			{
				int audible = 0;
				foreach ( var (_, voice) in ordered )
				{
					if ( voice.Delay > 0 ) { voice.Delay -= WaveFormat.Channels; continue; }
					if ( voice.Position == voice.Samples.Length )
					{
						if ( !voice.Loop ) continue;
						voice.Position = 0;
					}
					bool mix = audible++ < voiceLimit;
					for ( int channel = 0; channel < WaveFormat.Channels; ++channel )
					{
						float sample = voice.Samples[voice.Position++];
						if ( mix ) buffer[offset + i + channel] += sample * voice.Volume;
					}
				}
			}
			foreach ( var (handle, voice) in ordered )
				if ( !voice.Loop && voice.Position == voice.Samples.Length ) voices.Remove( handle );
		}
		for ( int i = offset; i < offset + count; ++i ) buffer[i] = Math.Clamp( buffer[i], -1, 1 );
		return count;
	}
}
