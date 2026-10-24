using System.Numerics;

namespace OpenTPW;

/// <summary>The advisor's independent screen-space model, as configured by TP.exe 0x429650.</summary>
public sealed class LobbyAdvisorGeometry
{
	public const string ModelPath = "global/advisor/advisor.md2";
	public ModelFile Model { get; }
	public Matrix4x4 RootTransform { get; }
	public static readonly System.Numerics.Vector3 ScreenOffset = new( 0.6f, -0.6f, 0.2f );

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
