namespace OpenTPW;

public class Sky : ModelEntity
{
	private Texture? colorTexture;
	private Vector3 color = new( 79 / 255f, 214 / 255f, 1 );

	/// <summary>Unlit sky RGB, in the range zero to one.</summary>
	public Vector3 Color
	{
		get => color;
		set => SetColor( value );
	}

	public unsafe void SetColor( Vector3 rgb01 )
	{
		if ( color == rgb01 ) return;
		color = rgb01;
		if ( colorTexture == null ) return;
		byte* rgba = stackalloc byte[4];
		rgba[0] = (byte)(Math.Clamp( color.X, 0, 1 ) * 255);
		rgba[1] = (byte)(Math.Clamp( color.Y, 0, 1 ) * 255);
		rgba[2] = (byte)(Math.Clamp( color.Z, 0, 1 ) * 255);
		rgba[3] = 255;
		Device.UpdateTexture( colorTexture.NativeTexture, (IntPtr)rgba, 4, 0, 0, 0, 1, 1, 1, 0, 0 );
	}

	public override void Spawn()
	{
		base.Spawn();
		colorTexture = new Texture( [79, 214, 255, 255], 1, 1 );
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		material.Set( "Color", colorTexture );
		Model = Primitives.Cube.GenerateModel( material );
		Scale = new Vector3( -10000f );
	}
}
