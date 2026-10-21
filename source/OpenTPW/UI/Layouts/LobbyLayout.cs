using OpenTPW.UI;

namespace OpenTPW;

internal sealed class LobbyLayout : Layout
{
	// The lobby's UI table (TP.exe 0x74f770, loaded by 0x4a4010) gives each element a rectangle in
	// units of 1/2048 of the screen width and 1/1536 of its height, which is 3.2 units per pixel at
	// 640x480. Each player button (0x7a14, 0x7a19, 0x7a1a, 0x7a1b) is 884,46 to 2048,193 moved
	// down 199 units per button, with its label (0x7a15) in 941,75 to 1716,164 on the same step.
	// The quit button (0x7a1c) is 1264,895 to 2428,1042 with its label in 1339,924 to 2039,1013.
	private const float UnitsPerPixel = 3.2f;
	private const float PlayerStep = 199;

	private static Rectangle FromUnits( float x0, float y0, float x1, float y1 )
	{
		return new Rectangle( x0 / UnitsPerPixel, y0 / UnitsPerPixel, (x1 - x0) / UnitsPerPixel, (y1 - y0) / UnitsPerPixel );
	}

	public override void OnInit()
	{
		Hud.AddChild( new Image( "/ui/textures/tpw_logo.wct" )
		{
			Uv0 = new Vector2( 0, 0.6f ),
			Uv1 = new Vector2( 1, 1 ),
			Position = new Vector2( 25 ),
			Size = new Vector2( 180f, 77.4f )
		} );

		string[] saves = [];
		if ( SaveFileSystem.DirectoryExists( "users" ) )
			saves = SaveFileSystem.GetDirectories( "users" );

		for ( int i = 0; i < 4; ++i )
		{
			var buttonText = "#CreateNewPlayer";

			if ( saves.Length > i )
			{
				buttonText = Path.GetFileName( saves[i] )[1..] ?? "#CreateNewPlayer";
			}

			var step = i * PlayerStep;
			Hud.AddChild( new PurpleButton( buttonText,
				FromUnits( 884, 46 + step, 2048, 193 + step ),
				FromUnits( 941, 75 + step, 1716, 164 + step ) ) );
		}

		Hud.AddChild( new PurpleButton( "#QuitGame",
			FromUnits( 1264, 895, 2428, 1042 ),
			FromUnits( 1339, 924, 2039, 1013 ) ) );
	}
}
