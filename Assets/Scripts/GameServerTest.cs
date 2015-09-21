using UnityEngine;
using Steamworks;

public class GameServerTest : MonoBehaviour {
	private void OnEnable() {
		// Initialize the SteamGameServer interface, we tell it some info about us, and we request support
		// for both Authentication (making sure users own games) and secure mode, VAC running in our game
		// and kicking users who are VAC banned
		bool ret = GameServer.Init(0, 8766, 27015, 27016, EServerMode.eServerModeNoAuthentication, "1.0.0.0");
		print("GameServer.Init() : " + ret);
		if (!ret) {
			return;			
		}
		
		// Set the "game dir".
		// This is currently required for all games.  However, soon we will be
		// using the AppID for most purposes, and this string will only be needed
		// for mods.  it may not be changed after the server has logged on
		SteamGameServer.SetModDir("spacewar");

		// These fields are currently required, but will go away soon.
		// See their documentation for more info
		SteamGameServer.SetProduct("SteamworksExample");
		SteamGameServer.SetGameDescription("Steamworks Example");

		// We don't support specators in our game.
		// .... but if we did:
		//SteamGameServer.SetSpectatorPort( ... );
		//SteamGameServer.SetSpectatorServerName( ... );

		//SteamGameServer.SetDedicatedServer(true);
		SteamGameServer.SetPasswordProtected(false);
		SteamGameServer.SetMaxPlayerCount(4);
		SteamGameServer.SetServerName("Test Server");
		SteamGameServer.SetMapName("Test Map");
		
		// Initiate Anonymous logon.
		// Coming soon: Logging into authenticated, persistent game server account
		SteamGameServer.LogOnAnonymous();

		// We want to actively update the master server with our presence so players can
		// find us via the steam matchmaking/server browser interfaces
		SteamGameServer.EnableHeartbeats(true);

		Debug.Log("Started.");
	}

	private void OnDisable() {
		// Notify Steam master server we are going offline
		SteamGameServer.EnableHeartbeats(false);

		// Disconnect from the steam servers
		SteamGameServer.LogOff();

		// release our reference to the steam client library
		GameServer.Shutdown();

		Debug.Log("Quit.");
	}
	
	private void Update() {
		GameServer.RunCallbacks();
	}

}
