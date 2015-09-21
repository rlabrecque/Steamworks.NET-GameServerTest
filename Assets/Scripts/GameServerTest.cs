// The Steamworks API's are modular, you can use some subsystems without using others
// When USE_GS_AUTH_API is defined you get the following Steam features:
// - Strong user authentication and authorization
// - Game server matchmaking
// - VAC cheat protection
// - Access to achievement/community API's
// - P2P networking capability

// Remove this define to disable using the native Steam authentication and matchmaking system
// You can use this as a sample of how to integrate your game without replacing an existing matchmaking system
// When you un-define USE_GS_AUTH_API you get:
// - Access to achievement/community API's
// - P2P networking capability
// You CANNOT use:
// - VAC cheat protection
// - Game server matchmaking
// as these function depend on using Steam authentication
#define USE_GS_AUTH_API 

using UnityEngine;
using Steamworks;

public class GameServerTest : MonoBehaviour {
	// Current game server version
	const string SPACEWAR_SERVER_VERSION = "1.0.0.0";

	// UDP port for the spacewar server to do authentication on (ie, talk to Steam on)
	const ushort SPACEWAR_AUTHENTICATION_PORT = 8766;

	// UDP port for the spacewar server to listen on
	const ushort SPACEWAR_SERVER_PORT = 27015;

	// UDP port for the master server updater to listen on
	const ushort SPACEWAR_MASTER_SERVER_UPDATER_PORT = 27016;

	//
	// Various callback functions that Steam will call to let us know about events related to our
	// connection to the Steam servers for authentication purposes.
	//
	// Tells us when we have successfully connected to Steam
	protected Callback<SteamServersConnected_t> m_CallbackSteamServersConnected;

	// Tells us when there was a failure to connect to Steam
	protected Callback<SteamServerConnectFailure_t> m_CallbackSteamServersConnectFailure;

	// Tells us when we have been logged out of Steam
	protected Callback<SteamServersDisconnected_t> m_CallbackSteamServersDisconnected;

	// Tells us that Steam has set our security policy (VAC on or off)
	protected Callback<GSPolicyResponse_t> m_CallbackPolicyResponse;

	//
	// Various callback functions that Steam will call to let us know about whether we should
	// allow clients to play or we should kick/deny them.
	//
	// Tells us a client has been authenticated and approved to play by Steam (passes auth, license check, VAC status, etc...)
	protected Callback<ValidateAuthTicketResponse_t> m_CallbackGSAuthTicketResponse;

	// client connection state
	protected Callback<P2PSessionRequest_t> m_CallbackP2PSessionRequest;
	protected Callback<P2PSessionConnectFail_t> m_CallbackP2PSessionConnectFail;

	public string m_strServerName = "Test Server";
	public string m_strMapName = "Milky Way";
	public int m_nMaxPlayers = 4;

	bool m_bInitialized;
	bool m_bConnectedToSteam;
	
	private void OnEnable() {
		m_CallbackSteamServersConnected = Callback<SteamServersConnected_t>.CreateGameServer(OnSteamServersConnected);
		m_CallbackSteamServersConnectFailure = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnSteamServersConnectFailure);
		m_CallbackSteamServersDisconnected = Callback<SteamServersDisconnected_t>.CreateGameServer(OnSteamServersDisconnected);
		m_CallbackPolicyResponse = Callback<GSPolicyResponse_t>.CreateGameServer(OnPolicyResponse);

		m_CallbackGSAuthTicketResponse = Callback<ValidateAuthTicketResponse_t>.CreateGameServer(OnValidateAuthTicketResponse);
		m_CallbackP2PSessionRequest = Callback<P2PSessionRequest_t>.CreateGameServer(OnP2PSessionRequest);
		m_CallbackP2PSessionConnectFail = Callback<P2PSessionConnectFail_t>.CreateGameServer(OnP2PSessionConnectFail);

		m_bInitialized = false;
		m_bConnectedToSteam = false;

#if USE_GS_AUTH_API
		EServerMode eMode = EServerMode.eServerModeAuthenticationAndSecure;
#else
		// Don't let Steam do authentication
		EServerMode eMode = EServerMode.eServerModeNoAuthentication;
#endif

		// Initialize the SteamGameServer interface, we tell it some info about us, and we request support
		// for both Authentication (making sure users own games) and secure mode, VAC running in our game
		// and kicking users who are VAC banned
		m_bInitialized = GameServer.Init(0, SPACEWAR_AUTHENTICATION_PORT, SPACEWAR_SERVER_PORT, SPACEWAR_MASTER_SERVER_UPDATER_PORT, eMode, SPACEWAR_SERVER_VERSION);
		if (!m_bInitialized) {
			Debug.Log("SteamGameServer_Init call failed");
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
		
		/*
		// We don't support specators in our game.
		// .... but if we did:
		//SteamGameServer.SetSpectatorPort( ... );
		//SteamGameServer.SetSpectatorServerName( ... );

		//SteamGameServer.SetDedicatedServer(true);
		SteamGameServer.SetPasswordProtected(false);
		SteamGameServer.SetMaxPlayerCount(4);
		SteamGameServer.SetServerName("Test Server");
		SteamGameServer.SetMapName("Test Map");*/

		// Initiate Anonymous logon.
		// Coming soon: Logging into authenticated, persistent game server account
		SteamGameServer.LogOnAnonymous();

		// We want to actively update the master server with our presence so players can
		// find us via the steam matchmaking/server browser interfaces
#if USE_GS_AUTH_API
		SteamGameServer.EnableHeartbeats(true);
#endif

		Debug.Log("Started.");
	}

	private void OnDisable() {
		// Notify Steam master server we are going offline
#if USE_GS_AUTH_API
		SteamGameServer.EnableHeartbeats(false);
#endif

		// Disconnect from the steam servers
		SteamGameServer.LogOff();

		// release our reference to the steam client library
		GameServer.Shutdown();

		Debug.Log("Shutdown.");
	}
	
	private void Update() {
		if(!m_bInitialized) {
			return;
		}

		GameServer.RunCallbacks();

		if(m_bConnectedToSteam) {
			SendUpdatedServerDetailsToSteam();
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: Take any action we need to on Steam notifying us we are now logged in
	//-----------------------------------------------------------------------------
	void OnSteamServersConnected(SteamServersConnected_t pLogonSuccess) {
		Debug.Log("SpaceWarServer connected to Steam successfully");
		m_bConnectedToSteam = true;

		// log on is not finished until OnPolicyResponse() is called

		// Tell Steam about our server details
		SendUpdatedServerDetailsToSteam();
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: Called when an attempt to login to Steam fails
	//-----------------------------------------------------------------------------
	void OnSteamServersConnectFailure(SteamServerConnectFailure_t pConnectFailure) {
		m_bConnectedToSteam = false;
		Debug.Log("SpaceWarServer failed to connect to Steam");
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: Called when we were previously logged into steam but get logged out
	//-----------------------------------------------------------------------------
	void OnSteamServersDisconnected(SteamServersDisconnected_t pLoggedOff) {
		m_bConnectedToSteam = false;
		Debug.Log("SpaceWarServer got logged out of Steam");
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: Callback from Steam when logon is fully completed and VAC secure policy is set
	//-----------------------------------------------------------------------------
	void OnPolicyResponse(GSPolicyResponse_t pPolicyResponse) {
#if USE_GS_AUTH_API
		// Check if we were able to go VAC secure or not
		if (SteamGameServer.BSecure()) {
			Debug.Log("SpaceWarServer is VAC Secure!");
		}
		else {
			Debug.Log("SpaceWarServer is not VAC Secure!");
		}
		
		Debug.Log("Game server SteamID:" + SteamGameServer.GetSteamID().ToString());
#endif
	}

	//-----------------------------------------------------------------------------
	// Purpose: Tells us Steam3 (VAC and newer license checking) has accepted the user connection
	//-----------------------------------------------------------------------------
	void OnValidateAuthTicketResponse(ValidateAuthTicketResponse_t pResponse) {
		Debug.Log("OnValidateAuthTicketResponse Called steamID: " + pResponse.m_SteamID); // Riley

		if (pResponse.m_eAuthSessionResponse == EAuthSessionResponse.k_EAuthSessionResponseOK) {
			// This is the final approval, and means we should let the client play (find the pending auth by steamid)
			/* TODO Riley: 
			for (uint i = 0; i < MAX_PLAYERS_PER_SERVER; ++i) {
				if (!m_rgPendingClientData[i].m_bActive)
					continue;
				else if (m_rgPendingClientData[i].m_SteamIDUser == pResponse.m_SteamID) {
					Debug.Log("Auth completed for a client");
					OnAuthCompleted(true, i);
					return;
				}
			}
			*/
		}
		else {
			// Looks like we shouldn't let this user play, kick them
			/* TODO Riley: 
			for (uint i = 0; i < MAX_PLAYERS_PER_SERVER; ++i) {
				if (!m_rgPendingClientData[i].m_bActive)
					continue;
				else if (m_rgPendingClientData[i].m_SteamIDUser == pResponse.m_SteamID) {
					Debug.Log("Auth failed for a client");
					OnAuthCompleted(false, i);
					return;
				}
			}
			*/
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: Handle clients connecting
	//-----------------------------------------------------------------------------
	void OnP2PSessionRequest(P2PSessionRequest_t pCallback) {
		Debug.Log("OnP2PSesssionRequest Called steamIDRemote: " + pCallback.m_steamIDRemote); // Riley

		// we'll accept a connection from anyone
		SteamGameServerNetworking.AcceptP2PSessionWithUser(pCallback.m_steamIDRemote);
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: Handle clients disconnecting
	//-----------------------------------------------------------------------------
	void OnP2PSessionConnectFail(P2PSessionConnectFail_t pCallback) {
		Debug.Log("OnP2PSessionConnectFail Called steamIDRemote: " + pCallback.m_steamIDRemote); // Riley

		// socket has closed, kick the user associated with it
		/* TODO Riley: 
		for (uint i = 0; i < MAX_PLAYERS_PER_SERVER; ++i) {
			// If there is no ship, skip
			if (!m_rgClientData[i].m_bActive)
				continue;

			if (m_rgClientData[i].m_SteamIDUser == pCallback.m_steamIDRemote) {
				Debug.Log("Disconnected dropped user");
				RemovePlayerFromServer(i);
				break;
			}
		}*/
	}


	//-----------------------------------------------------------------------------
	// Purpose: Called once we are connected to Steam to tell it about our details
	//-----------------------------------------------------------------------------
	void SendUpdatedServerDetailsToSteam() {
		//
		// Set state variables, relevant to any master server updates or client pings
		//

		// These server state variables may be changed at any time.  Note that there is no lnoger a mechanism
		// to send the player count.  The player count is maintained by steam and you should use the player
		// creation/authentication functions to maintain your player count.
		SteamGameServer.SetMaxPlayerCount(m_nMaxPlayers);
		SteamGameServer.SetPasswordProtected(false);
		SteamGameServer.SetServerName(m_strServerName);
		SteamGameServer.SetBotPlayerCount(0); // optional, defaults to zero
		SteamGameServer.SetMapName(m_strMapName);

#if USE_GS_AUTH_API
		// Update all the players names/scores
		/* @TODO: Riley: Disabled
		for (uint32 i = 0; i < MAX_PLAYERS_PER_SERVER; ++i) {
			if (m_rgClientData[i].m_bActive && m_rgpShips[i]) {
				SteamGameServer.BUpdateUserData(m_rgClientData[i].m_SteamIDUser, m_rgpShips[i]->GetPlayerName(), m_rguPlayerScores[i]);
			}
		}*/
#endif

		// game type is a special string you can use for your game to differentiate different game play types occurring on the same maps
		// When users search for this parameter they do a sub-string search of this string 
		// (i.e if you report "abc" and a client requests "ab" they return your server)
		//SteamGameServer.SetGameType( "dm" );

		// update any rule values we publish
		//SteamGameServer.SetKeyValue( "rule1_setting", "value" );
		//SteamGameServer.SetKeyValue( "rule2_setting", "value2" );
	}
}
