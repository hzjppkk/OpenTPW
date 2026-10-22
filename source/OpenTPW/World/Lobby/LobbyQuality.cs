using System.Globalization;

namespace OpenTPW;

/// <summary>The lobby subset of a retail quality preset.</summary>
public sealed class LobbyQuality
{
	public string Preset { get; }
	public int ObjectsPercent { get; }
	public int TextureQuality { get; }
	public bool WeatherEnabled { get; }
	public bool UseSmallTextures => TextureQuality <= 1;

	private LobbyQuality( string preset, SettingsFile settings )
	{
		Preset = preset;
		ObjectsPercent = Read( settings, "GameOptions.LOBBYOBJECTS", 0, 100 );
		TextureQuality = Read( settings, "GraphicalOptions.TEXTUREQUALITY", 0, 3 );
		WeatherEnabled = Read( settings, "GameOptions.WEATHER", 0, 1 ) != 0;
	}

	/// <summary>Reads the selected preset; the retail initial detail level is medium (0x423170).</summary>
	public static LobbyQuality Load( string? preset = null )
	{
		preset = (preset ?? Settings.Default.LobbyQuality).ToLowerInvariant();
		if ( preset is not ("low" or "med" or "high") )
			throw new ArgumentException( $"Unknown lobby quality preset: {preset}", nameof( preset ) );
		return new LobbyQuality( preset, new SettingsFile( $"/{preset}.sam" ) );
	}

	private static int Read( SettingsFile settings, string key, int minimum, int maximum )
	{
		if ( !int.TryParse( settings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value ) || value < minimum || value > maximum )
			throw new InvalidDataException( $"Quality setting {key} must be an integer from {minimum} to {maximum}." );
		return value;
	}

	public string TextureDirectory( string directory )
	{
		// 0x54a300 -> 0x467430 switches the directory, not individual missing textures.
		directory = directory.Replace( '\\', '/' ).TrimEnd( '/' );
		if ( !UseSmallTextures ) return directory;
		int slash = directory.LastIndexOf( '/' );
		return directory[(slash + 1)..].Equals( "textures", StringComparison.OrdinalIgnoreCase )
			? directory[..(slash + 1)] + "stexture" : directory;
	}
}
