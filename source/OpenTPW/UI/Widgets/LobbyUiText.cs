using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// BF4 text in a retail 2048x1536 control rectangle (3.2 units per 640x480 pixel), clipped to that rectangle.
/// Alignment 0/1/2 = start/centre/end, as passed to 0x656a78.
/// </summary>
internal sealed class LobbyUiText : Panel
{
	private readonly FontFile font;
	private readonly Rectangle units;
	private readonly Dictionary<(string Text, uint Color), Texture> textures = new();
	public string Text { get; set; }
	public uint Color { get; set; }
	public Vector2 Offset { get; set; }
	public int HorizontalAlignment { get; set; } = 1;
	public int VerticalAlignment { get; set; } = 1;
	/// <summary>Horizontal scroll in 640x480 pixels, used by the name box to keep the caret visible.</summary>
	public float Scroll { get; set; }
	public float Measure( string text ) => font.MeasureString( text );
	public int LineHeight => font.LineHeight - font.Top;
	public LobbyUiText( string text, Rectangle units, string fontName = "GAME7", uint color = 0xffffffff )
	{
		font = new FontFile( GameLanguage.Path( $"{fontName}.bf4" ) );
		this.units = units;
		Text = text;
		Color = color;
	}
	protected override void OnRender()
	{
		if ( Text.Length == 0 ) return;
		var lines = Text.Replace( "\r\n", "\n" ).Replace( '\r', '\n' ).Split( '\n' );
		float top = units.Y / 3.2f + (units.Height / 3.2f - LineHeight * lines.Length) * VerticalAlignment / 2 + Offset.Y;
		for ( int line = 0; line < lines.Length; line++ ) DrawLine( lines[line], top + line * LineHeight );
	}
	private void DrawLine( string text, float y )
	{
		if ( text.Length == 0 ) return;
		if ( !textures.TryGetValue( (text, Color), out var texture ) )
		{
			var mask = font.RenderString( text, Color );
			textures.Add( (text, Color), texture = new Texture( mask.Data, mask.Width, mask.Height, TextureFlags.PointFilter ) );
		}
		float x = units.X / 3.2f + (units.Width / 3.2f - font.MeasureString( text )) * HorizontalAlignment / 2 + Offset.X - Scroll;
		float left = Math.Max( x, units.X / 3.2f ), right = Math.Min( x + texture.Width, (units.X + units.Width) / 3.2f );
		if ( right <= left ) return;
		var material = Material.UI;
		material.Set( "Color", texture );
		Graphics.Quad( new Rectangle( left * 2, 720 - (y + texture.Height) * 1.5f, (right - left) * 2, texture.Height * 1.5f ),
			new Rectangle( (left - x) / texture.Width, 0, (right - left) / texture.Width, 1 ), material );
	}
}
