using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// A lobby button: the purple bar and its label. Rectangles are in the original game's
/// 640x480 screen space (see <see cref="LobbyLayout"/>).
/// </summary>
internal class PurpleButton : Panel
{
	// The original lays the lobby out for 640x480; this HUD is 1280x720. Scale to fit the
	// height and keep the buttons against the right edge, where the original puts them.
	private const float Scale = 720f / 480f;

	// Label style, set for every lobby label by the lobby setup (TP.exe 0x4a4010):
	// font slot 5 (0x483d20), which is TITLEMED.bf4 at 640x480 (the font loader 0x419390 fills
	// set 1, the set the game uses at that resolution, with resource 0x40d = TITLEMED.bf4 in
	// residx.dat), and the colour at 0x74fbc8..0x74fbd0 (0xEC, 0xA2, 0x04, alpha 0xFF).
	private const string FontPath = "Language/English/TITLEMED.bf4";
	private const uint LabelColor = 0xECA204FF;

	// The text element draws each label twice (TP.exe 0x48f950): first a shadow with texel
	// (0x80, 0x80, 0x80, 0x80), 2 pixels left and 2 down, then the text with (0xFF, 0xFF, 0xFF, 0xFF).
	// Both are tinted by the label colour.
	private const int ShadowOffset = 2;
	private const byte ShadowLevel = 0x80;

	// Where the bar's visible top-left edge sits in the original, inside the button's rectangle
	// (measured on the original lobby at 640x480: rectangle 276.25,14.38; bar edge 280,16)
	private static readonly Vector2 BarInset = new( 3.75f, 1.6f );

	// Offset from the bar's visible top-left edge to the position the bar quads below are drawn
	// from (measured on this HUD). The original draws the bar as a model (ui.wad b_login.MD2);
	// these quads only approximate it.
	private static readonly Vector2 BarOrigin = new( 546, 30 );

	private Texture ButtonText1;
	private Texture ButtonText2;
	private Texture Label;
	private Rectangle LabelRect;

	private static FontFile Font;

	public string Text { get; set; }

	/// <param name="frame">The button's rectangle, 640x480 space</param>
	/// <param name="textBox">The rectangle the label is centred in, 640x480 space</param>
	public PurpleButton( string text, Rectangle frame, Rectangle textBox )
	{
		ButtonText1 = new Texture( "ui/textures/button_text1.wct", TextureFlags.PointFilter );
		ButtonText2 = new Texture( "ui/textures/button_text2.wct", TextureFlags.PointFilter );

		Text = Localization.Parse( text );
		Log.Info( $"Parsed '{text}' as '{Text}'" );

		Font ??= new FontFile( FontPath );
		var label = RenderLabel( Font, Text );
		Label = new Texture( label.Data, label.Width, label.Height, TextureFlags.PointFilter );

		// Centre the text (not its shadow) in the text box; the shadow hangs off its left and bottom
		var textSize = new Vector2( Font.MeasureString( Text ), Font.LineHeight - Font.Top );
		var textTopLeft = new Vector2( textBox.X + (textBox.Width - textSize.X) / 2, textBox.Y + (textBox.Height - textSize.Y) / 2 );
		var labelTopLeft = ToHud( textTopLeft - new Vector2( ShadowOffset, 0 ) );
		var labelSize = new Vector2( label.Width, label.Height ) * Scale;
		LabelRect = new Rectangle( labelTopLeft.X, labelTopLeft.Y - labelSize.Y, labelSize.X, labelSize.Y );

		position = ToHud( new Vector2( frame.X, frame.Y ) + BarInset ) + BarOrigin;
	}

	/// <summary>
	/// A point in the original's 640x480 screen space (y down) on this HUD (y up).
	/// </summary>
	private static Vector2 ToHud( Vector2 point )
	{
		return new Vector2( 1280 - (640 - point.X) * Scale, 720 - point.Y * Scale );
	}

	/// <summary>
	/// The label with its shadow, as the text element draws it (see <see cref="ShadowOffset"/>).
	/// </summary>
	private static TextureData RenderLabel( FontFile font, string text )
	{
		var mask = font.RenderString( text );
		int width = mask.Width + ShadowOffset, height = mask.Height + ShadowOffset;
		var data = new byte[width * height * 4];

		void Put( int x, int y, byte level, byte alpha )
		{
			int o = (y * width + x) * 4;
			data[o] = (byte)((LabelColor >> 24) * level / 255);
			data[o + 1] = (byte)(((LabelColor >> 16) & 0xFF) * level / 255);
			data[o + 2] = (byte)(((LabelColor >> 8) & 0xFF) * level / 255);
			data[o + 3] = (byte)((LabelColor & 0xFF) * alpha / 255);
		}

		for ( int pass = 0; pass < 2; pass++ )
		{
			for ( int y = 0; y < mask.Height; y++ )
			{
				for ( int x = 0; x < mask.Width; x++ )
				{
					var coverage = mask.Data[(y * mask.Width + x) * 4 + 3];
					if ( coverage == 0 )
						continue;

					if ( pass == 0 )
						Put( x, y + ShadowOffset, ShadowLevel, (byte)(ShadowLevel * coverage / 255) );
					else
						Put( x + ShadowOffset, y, 255, coverage );
				}
			}
		}

		return new TextureData( width, height, data );
	}

	protected override void OnRender()
	{
		var material = Material.UI;
		var size = new Vector2( 200, 100 );
		var rect = new Rectangle( position, size );

		var uvs = new Rectangle( 0f, 0f, 1.0f, 0.5f );
		rect.Y -= 130;
		rect.X -= 740;

		//
		// End
		//
		material.Set( "Color", ButtonText1 );
		rect.X += 190;
		Graphics.Quad( rect, uvs.Shift( new Vector2( 0, 0.50f ) ), material );

		//
		// Middle
		//
		material.Set( "Color", ButtonText1 );
		rect.X += 190;
		Graphics.Quad( rect.Shift( new Vector2( 0, 28 ) ), uvs.Shift( new Vector2( 0, 0f ) ), material );

		//
		// Start
		//
		rect.X += 200;
		material.Set( "Color", ButtonText2 );
		Graphics.Quad( rect, uvs.Shift( new Vector2( 0f, 0.50f ) ), material );

		//
		// Label
		//
		material.Set( "Color", Label );
		Graphics.Quad( LabelRect, new Rectangle( 0, 0, 1, 1 ), material );
	}
}
