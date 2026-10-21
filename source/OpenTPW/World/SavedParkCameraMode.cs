namespace OpenTPW;

public sealed class SavedParkCameraMode : CameraMode
{
	public static Vector3 Target { get; set; }

	public override void Update()
	{
		Position = Target + new Vector3( 0, -100, 90 );
		Rotation = Rotation.LookAt( Target - Position );
		FieldOfView = 65;
	}
}
