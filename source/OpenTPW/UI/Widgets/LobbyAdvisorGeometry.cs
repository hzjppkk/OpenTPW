using System.Numerics;

namespace OpenTPW;

/// <summary>The advisor's independent screen-space model, as configured by TP.exe 0x429650.</summary>
public sealed class LobbyAdvisorGeometry
{
	public const string ModelPath = "global/advisor/advisor.md2";
	public ModelFile Model { get; }
	public Matrix4x4 RootTransform { get; }
	public static readonly System.Numerics.Vector3 ScreenOffset = new( 0.6f, -0.6f, 0.2f );

	/// <summary>
	/// The advisor's own directional light (record +0x48: constructor 0x429510 sets type 2 and colour 1.0;
	/// 0x429650 aims it along (26.67, −26.67, 800) and registers it with mask 0x10). 0x5731a8 draws each UI
	/// list with only its own lights current (engine +0x30), so the advisor is lit by this light alone,
	/// plus the global ambient colour.
	/// </summary>
	public static readonly System.Numerics.Vector3 LightDirection = System.Numerics.Vector3.Normalize( new( 26.666666f, -26.666666f, 800 ) );

	/// <summary>Vertex colour from <see cref="LightDirection"/> for a node-to-screen matrix and an authored normal (0x5707f0, 0x5709e0).</summary>
	public static System.Numerics.Vector3 Colour( System.Numerics.Vector3 ambient, Matrix4x4 node, System.Numerics.Vector3 normal )
	{
		if ( !Matrix4x4.Invert( node, out var inverse ) ) throw new InvalidDataException( "Advisor node matrix cannot be inverted for lighting" );
		var local = System.Numerics.Vector3.TransformNormal( LightDirection, inverse );
		float length = local.LengthSquared();
		if ( length is not (0 or 1) ) local /= MathF.Sqrt( length );
		float facing = MathF.Max( 0, -System.Numerics.Vector3.Dot( local, normal ) );
		return System.Numerics.Vector3.Min( ambient + new System.Numerics.Vector3( facing ), System.Numerics.Vector3.One );
	}

	public LobbyAdvisorGeometry()
	{
		byte[] bytes = FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( ModelPath ) );
		Model = new ModelFile( new MemoryStream( bytes ) );
		var root = Model.Nodes.Single( node => node.ParentIndex < 0 );
		// 0x46e210 scales the local basis rows, before the authored root rotation.
		root.LocalTransform = Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f ) * root.LocalTransform;
		RootTransform = root.LocalTransform;
	}

	public ModelPose Pose( ModelAnimation animation, float frame )
	{
		return animation.Evaluate( Model, frame, ModelAnimation.SegmentHiding.NodeId );
	}

	public static Vector3 Project( System.Numerics.Vector3 point )
	{
		point += ScreenOffset;
		return new Vector3( point.X, point.Y, point.Z );
	}
}
