namespace OpenTPW;

public sealed class LobbyGlobeCameraMode : CameraMode
{
	internal static LobbyGlobeState State { get; set; } = null!;
	public override void Update()
	{
		State.Update( RetailTime.FrameTicks, new System.Numerics.Vector2( Input.Mouse.Position.X / Screen.Size.X, Input.Mouse.Position.Y / Screen.Size.Y ) );
		Position = State.Position;
		Rotation = Rotation.LookAt( -Position );
		FieldOfView = (2 * MathF.Atan( 0.5f * MathF.Tan( 60f.DegreesToRadians() * 0.5f ) )).RadiansToDegrees();
	}
}
