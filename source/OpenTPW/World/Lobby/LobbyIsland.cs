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
		islandAnimations = Enumerable.Range( 1, 2 )
			.Select( i => $"{config.ResourceDirectory}/{config.ModelName}M{i}.md2" )
			.Where( FileSystem.FileExists ).ToArray();
		island = new LobbyModel( Position, config.ModelPath, textures, textureResolver:
			name => LobbySigns.Resolve( config.ModelPath, name, config.Name ) ) { Rotation = Rotation };
		if ( islandAnimations.Length > 0 ) island.Play( islandAnimations[0], false );
		gate = new LobbyModel( Position, config.GateModelPath, textures, textureResolver:
			name => LobbySigns.Resolve( config.GateModelPath, name, config.Name ) ) { Rotation = Rotation };
		var gateAnimation = $"{config.ResourceDirectory}/{config.GateModelName}M1.md2";
		if ( FileSystem.FileExists( gateAnimation ) ) gate.Play( gateAnimation );
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
