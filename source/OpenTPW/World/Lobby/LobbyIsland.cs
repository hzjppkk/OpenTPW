using System.Numerics;

namespace OpenTPW;

/// <summary>An island and its gate share the ISLAND placement and model rotation.</summary>
public sealed class LobbyIsland : Entity
{
	private readonly LobbyModel island;
	private readonly LobbyModel gate;
	private readonly string[] islandAnimations;
	private int animationIndex;

	public LobbyIsland( Vector3 position, LobbyIslandConfig config )
	{
		var placement = GetPlacement( position, config );
		Position = placement.Position;
		Rotation = placement.Rotation;
		var textures = $"{config.ResourceDirectory}/textures";
		islandAnimations = AnimationFiles.Segments( config.ModelPath, AnimationSlot.M ).ToArray();
		// A configured island requires segment zero; missing animation is a data error.
		_ = AnimationFiles.Resolve( config.ModelPath, AnimationSlot.M, 0 );
		island = new LobbyModel( Position, config.ModelPath, textures, textureResolver:
			name => LobbySigns.Resolve( config.ModelPath, name, config.Name ) ) { Rotation = Rotation };
		if ( islandAnimations.Length > 0 ) island.Play( islandAnimations[0], false );
		gate = new LobbyModel( Position, config.GateModelPath, textures, textureResolver:
			name => LobbySigns.Resolve( config.GateModelPath, name, config.Name ) ) { Rotation = Rotation };
		gate.Play( AnimationFiles.Resolve( config.GateModelPath, AnimationSlot.M, 0 ) );
		uint seed = (uint)(config.Id + 1) * 0x12345u;
		foreach ( var mesh in config.FlyingMeshes )
			_ = new LobbyFlock( mesh, placement.Target, 70, seed++ );
	}

	internal static (Vector3 Position, Vector3 Target, Quaternion Rotation) GetPlacement( Vector3 position, LobbyIslandConfig config )
	{
		// 0x5daa20 retains the ISLAND height as the orbit/flock anchor, but sets both model roots to Y=0.
		var modelPosition = new Vector3( position.X, position.Y, 0 );
		var target = new Vector3( position.X, position.Y, config.Height );
		// Retail +Y rotation becomes -Z after swapping retail Y/Z (0x5d33c0 -> 0x46ddf0).
		var rotation = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -config.RotationDegrees.DegreesToRadians() );
		return (modelPosition, target, rotation);
	}

	protected override void OnUpdate()
	{
		if ( islandAnimations.Length > 0 && island.AnimationFinished )
		{
			animationIndex = (animationIndex + 1) % islandAnimations.Length;
			island.Play( islandAnimations[animationIndex], false );
		}
	}
}
