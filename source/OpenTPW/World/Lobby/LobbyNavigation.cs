namespace OpenTPW;

internal static class LobbyNavigation
{
	public static void Draw( LobbyConfig config )
	{
		// Retail 0x5dc310 dispatches arrows/Return to the active controller.
		// The login controller navigates its player list, not the four themes.
		// Player binding supplies SelectIsland/BeginEntering when implemented.
	}
}
