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
		Position = position + new Vector3( 0, 0, config.Height );
		Rotation = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -config.RotationDegrees.DegreesToRadians() );
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
			_ = new LobbyFlock( mesh, Position, 70, seed++ );
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
