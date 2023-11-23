using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public enum CameraAngles
{
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2
}

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { set; get; }
    public Server server;
    public Client client;

    [SerializeField]
    Animator menuAnimatior;

    [SerializeField]
    TMP_InputField addressInput;

    [SerializeField]
    GameObject[] cameraAngles;

    public Action<bool> SetLocalGame;
    public Action SetMatchMinimaxGame;
    public Action SetMatchReinforceGame;

    void Awake()
    {
        Instance = this;
        ChangeCamera((int)CameraAngles.menu);
        RegisterEvents();
    }

    public void ChangeCamera(CameraAngles index)
    {
        for (int i = 0; i < cameraAngles.Length; i++)
            cameraAngles[i].SetActive(false);
        cameraAngles[(int)index].SetActive(true);
    }

    public void OnMatchMinimaxGameButton()
    {
        SetMatchMinimaxGame?.Invoke();
        menuAnimatior.SetTrigger("InGameMenu");
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
    }

    public void OnMatchReinforceGameButton()
    {
        SetMatchReinforceGame?.Invoke();
        menuAnimatior.SetTrigger("InGameMenu");
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
    }

    public void OnOnlineGameButton()
    {
        menuAnimatior.SetTrigger("OnlineMenu");
    }

    public void OnOnlineHostButton()
    {
        SetLocalGame?.Invoke(false);
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
        menuAnimatior.SetTrigger("HostMenu");
    }

    public void OnOnlineConnectButton()
    {
        SetLocalGame?.Invoke(false);
        client.Init(addressInput.text, 8007);
    }

    public void OnOnlineBackButton()
    {
        menuAnimatior.SetTrigger("StartMenu");
    }

    public void OnLeaveFromGameMenu()
    {
        ChangeCamera(CameraAngles.menu);
        menuAnimatior.SetTrigger("StartMenu");
    }

    public void OnHostBackButton()
    {
        server.Shutdown();
        client.Shutdown();
        menuAnimatior.SetTrigger("OnlineMenu");
    }

    #region
    void RegisterEvents()
    {
        NetUtility.C_START_GAME += OnStartGameClient;
    }

    void UnregisterEvents()
    {
        NetUtility.C_START_GAME -= OnStartGameClient;
    }

    void OnStartGameClient(NetMessage msg)
    {
        menuAnimatior.SetTrigger("InGameMenu");
    }
    #endregion
}
