using OpenTPW.UI;

namespace OpenTPW;

internal sealed class LobbyUiText : Panel
{
	private readonly FontFile font;
	private readonly Rectangle units;
	private readonly Dictionary<string, Texture> textures = new();
	public string Text { get; set; }
	public LobbyUiText( string text, Rectangle units, string fontName = "GAME7" )
	{
		font = new FontFile( $"Language/English/{fontName}.bf4" );
		this.units = units;
		Text = text;
	}
	protected override void OnRender()
	{
		if ( Text.Length == 0 ) return;
		if ( !textures.TryGetValue( Text, out var texture ) )
		{
			var mask = font.RenderString( Text );
			textures.Add( Text, texture = new Texture( mask.Data, mask.Width, mask.Height, TextureFlags.PointFilter ) );
		}
		float x = units.X / 3.2f + (units.Width / 3.2f - font.MeasureString( Text )) / 2;
		float y = units.Y / 3.2f + (units.Height / 3.2f - texture.Height) / 2;
		var material = Material.UI;
		material.Set( "Color", texture );
		Graphics.Quad( new Rectangle( x * 2, 720 - (y + texture.Height) * 1.5f, texture.Width * 2, texture.Height * 1.5f ), new Rectangle( 0, 0, 1, 1 ), material );
	}
}
