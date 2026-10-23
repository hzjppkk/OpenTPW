using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// A lobby button: the purple bar and its label. Rectangles are in the original game's
/// 640x480 screen space (see <see cref="LobbyLayout"/>).
/// </summary>
internal class PurpleButton : Panel
{
	// TP.exe 0x474f10 maps each axis independently from its 2048x1536 control space.
	// Expressed in 640x480 pixels, the current 1280x720 HUD uses scales 2 and 1.5.
	private static readonly Vector2 Scale = new( 2, 1.5f );

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

	private readonly List<Model> bars = new();
	private static LobbyButtonGeometry? geometry;
	private static Shader? barShader;
	private static readonly Dictionary<string, Texture> barTextures = new( StringComparer.OrdinalIgnoreCase );

	private Texture Label;
	private Rectangle LabelRect;

	private static FontFile Font;

	public string Text { get; set; }

	/// <param name="frame">The button's rectangle, 640x480 space</param>
	/// <param name="textBox">The rectangle the label is centred in, 640x480 space</param>
	public PurpleButton( string text, Rectangle frame, Rectangle textBox )
	{
		CreateBar( frame );

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


	}

	/// <summary>
	/// A point in the original's 640x480 screen space (y down) on this HUD (y up).
	/// </summary>
	private static Vector2 ToHud( Vector2 point )
	{
		return new Vector2( point.X * Scale.X, 720 - point.Y * Scale.Y );
	}

	private void CreateBar( Rectangle frame )
	{
		geometry ??= new LobbyButtonGeometry( FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( "ui/b_login.md2" ) ) );
		barShader ??= new Shader( "content/shaders/ui.shader" );
		foreach ( var mesh in geometry.Model.Meshes )
		{
			var vertices = new Vertex[mesh.Vertices.Length];
			for ( int i = 0; i < vertices.Length; i++ )
			{
				var point = System.Numerics.Vector3.Transform( mesh.Vertices[i].Position.GetSystemVector3(), mesh.WorldTransform );
				vertices[i] = new Vertex( geometry.Project( point, frame ), mesh.TexCoords[i] );
			}
			for ( int m = 0; m < mesh.Materials.Length; m++ )
			{
				var indices = new List<uint>();
				for ( int i = 0; i < mesh.Indices.Length; i += 3 )
				{
					if ( mesh.Vertices[mesh.Indices[i]].TextureIndex != m ) continue;
					indices.AddRange( mesh.Indices.Skip( i ).Take( 3 ) );
				}
				if ( indices.Count == 0 ) continue;
				string name = mesh.Materials[m].Name?.TrimEnd( '\0' )
					?? throw new InvalidDataException( "Lobby button material has no texture" );
				if ( !barTextures.TryGetValue( name, out var texture ) )
					barTextures[name] = texture = new Texture( AnimationFiles.ResolveFile( $"ui/textures/{name}.wct", "Lobby button texture" ), TextureFlags.PointFilter );
				var material = new Material<ObjectUniformBuffer>( barShader, MaterialFlags.DisableDepth | MaterialFlags.DisableCulling );
				material.Set( "Color", texture );
				bars.Add( new Model( vertices, indices.ToArray(), material ) );
			}
		}
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
		foreach ( var bar in bars ) bar.Draw();

		material.Set( "Color", Label );
		Graphics.Quad( LabelRect, new Rectangle( 0, 0, 1, 1 ), material );
	}
}
