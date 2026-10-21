using ImGuiNET;

namespace OpenTPW;

internal static class LobbyNavigation
{
	public static void Draw( LobbyConfig config )
	{
		ImGui.SetNextWindowPos( new System.Numerics.Vector2( 20, Screen.Size.Y - 90 ), ImGuiCond.Always );
		ImGui.SetNextWindowBgAlpha( 0.75f );
		ImGui.Begin( "Islands", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings );
		foreach ( var island in config.Islands )
		{
			if ( island.Id != config.Islands[0].Id ) ImGui.SameLine();
			if ( ImGui.Selectable( island.Name, LobbyCameraMode.SelectedIsland == island.Id, ImGuiSelectableFlags.None,
				new System.Numerics.Vector2( 115, 24 ) ) ) LobbyCameraMode.SelectIsland( island.Id );
		}
		ImGui.End();
	}
}
