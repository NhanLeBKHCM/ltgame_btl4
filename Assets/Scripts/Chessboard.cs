using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Numerics;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;
using Plane = UnityEngine.Plane;
using Unity.Networking.Transport;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    public static Chessboard Instance { set; get; }

    [Header("Art stuff")]
    [SerializeField]
    Material tileMaterial;

    [SerializeField]
    float tileSize = 1.5f;

    [SerializeField]
    float yOffset = 0.1f;

    [SerializeField]
    Vector3 boardCenter = Vector3.zero;

    [SerializeField]
    float deathSize = 0.3f;

    [SerializeField]
    float deathSpacing = 0.5f;

    [SerializeField]
    float dragOffset = 3.3f;

    [SerializeField]
    GameObject VictoryScreen;

    [SerializeField]
    Transform rematchIndicator;

    [SerializeField]
    Button rematchButton;

    [Header("Prefabs & Materials")]
    [SerializeField]
    GameObject[] prefabs;

    [SerializeField]
    Material[] teamMaterials;
    ChessPiece[,] chessPieces;
    ChessPiece currentlyDragging;
    List<Vector2Int> availableMoves = new List<Vector2Int>();
    List<ChessPiece> deadWhites = new List<ChessPiece>();
    List<ChessPiece> deadBlacks = new List<ChessPiece>();
    const int TILE_COUNT_X = 8;
    const int TILE_COUNT_Y = 8;
    GameObject[,] tiles;
    Camera currentCamera;
    Vector2Int currentHover;
    Vector3 bounds = Vector3.zero;
    bool isWhiteTurn;
    SpecialMove specialMove;
    List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    int playerCount = -1;
    int currentTeam = -1;
    bool localGame = true;
    bool[] playerRematch = new bool[2];
    Bot bot;

    void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }
        if (localGame && currentTeam == 1)
        {
            (Vector2Int, Vector2Int) step = bot.GetStep(1, chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            Debug.Log($"MoveTo ({step.Item1.x},{step.Item1.y})->({step.Item2.x},{step.Item2.y})");
            MoveTo(step.Item1.x, step.Item1.y, step.Item2.x, step.Item2.y);
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer =
                    (ContainsValidMove(ref availableMoves, currentHover))
                        ? LayerMask.NameToLayer("Highlight")
                        : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    if (
                        (
                            chessPieces[hitPosition.x, hitPosition.y].team == 0
                            && isWhiteTurn
                            && currentTeam == 0
                        )
                        || (
                            chessPieces[hitPosition.x, hitPosition.y].team == 1
                            && !isWhiteTurn
                            && currentTeam == 1
                        )
                    )
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        availableMoves = currentlyDragging.GetAvailableMoves(
                            ref chessPieces,
                            TILE_COUNT_X,
                            TILE_COUNT_Y
                        );

                        specialMove = currentlyDragging.GetSpecialMoves(
                            ref chessPieces,
                            ref moveList,
                            ref availableMoves
                        );

                        PreventCheck(
                            chessPieces,
                            ref availableMoves,
                            currentlyDragging,
                            TILE_COUNT_X,
                            TILE_COUNT_Y
                        );
                        HighlightTiles();
                    }
                }
                else
                    currentlyDragging = null;
            }
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(
                    currentlyDragging.currentX,
                    currentlyDragging.currentY
                );
                if (
                    ContainsValidMove(
                        ref availableMoves,
                        new Vector2Int(hitPosition.x, hitPosition.y)
                    )
                )
                {
                    MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    NetMakeMove nm = new NetMakeMove();
                    nm.originalX = previousPosition.x;
                    nm.originalY = previousPosition.y;
                    nm.destinationX = hitPosition.x;
                    nm.destinationY = hitPosition.y;
                    nm.teamId = currentTeam;
                    Client.Instance.SendToServer(nm);
                }
                else
                {
                    currentlyDragging.SetPosition(
                        GetTileCenter(previousPosition.x, previousPosition.y)
                    );
                }
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer =
                    (ContainsValidMove(ref availableMoves, currentHover))
                        ? LayerMask.NameToLayer("Highlight")
                        : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }
            if (currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(
                    GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY)
                );
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }
        if (currentlyDragging)
        {
            Plane horizonPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizonPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }

    void Start()
    {
        Instance = this;
        isWhiteTurn = true;
        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }

    void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds =
            new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }

    GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;
        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vectices = new Vector3[4];
        vectices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vectices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vectices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vectices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.vertices = vectices;
        mesh.triangles = tris;

        // mesh.RecalculateNormals();
        tileObject.layer = LayerMask.NameToLayer("Tile");

        tileObject.AddComponent<BoxCollider>();
        return tileObject;
    }

    void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0,
            blackTeam = 1;

        // whiteTeam
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);

        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        // blackTeam
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);

        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
    }

    public ChessPiece SpawnSinglePiece(ChessPieceType type, int team, bool visible = true)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];
        cp.transform.gameObject.SetActive(visible);
        if (team == 0)
            cp.transform.Rotate(0.0f, 180.0f, 0.0f, Space.Self);
        return cp;
    }

    void MoveTo(int originalX, int originalY, int x, int y)
    {
        ChessPiece cp = chessPieces[originalX, originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);
        Debug.Log($"{cp.team},{originalX},{originalY}");
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];
            if (cp.team == ocp.team)
                return;
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);
                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(
                    new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                        - bounds
                        + new Vector3(tileSize / 2, 0, tileSize / 2)
                        + (Vector3.forward * deathSpacing) * deadWhites.Count
                );
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);
                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(
                    new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                        - bounds
                        + new Vector3(tileSize / 2, 0, tileSize / 2)
                        + (Vector3.back * deathSpacing) * deadBlacks.Count
                );
            }
        }

        chessPieces[previousPosition.x, previousPosition.y] = null;
        chessPieces[x, y] = cp;

        PositionSinglePieces(x, y);
        isWhiteTurn = !isWhiteTurn;
        if (localGame)
            currentTeam = (currentTeam == 0) ? 1 : 0;
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });

        ProcessSpecialMove();
        if (CheckForCheckMate())
            CheckMate(cp.team);
        return;
    }

    void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSinglePieces(x, y, true);
    }

    void PositionSinglePieces(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer(
                "Highlight"
            );
        }
    }

    void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }

    bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        }
        return false;
    }

    Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize)
            - bounds
            + new Vector3(tileSize / 2, 0, tileSize / 2);
    }

    void CheckMate(int team)
    {
        DisplayVictory(team);
    }

    void DisplayVictory(int winningTeam)
    {
        VictoryScreen.SetActive(true);
        VictoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }

    public void OnRematchButton()
    {
        if (localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);

            currentTeam = 0;
        }
        else
        {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }

    public void GameReset()
    {
        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        VictoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        VictoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        VictoryScreen.SetActive(false);

        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);
                chessPieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);
        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
    }

    public void OnMenuButton()
    {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        Debug.Log("Reset");
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutdownRelay", 1.0f);

        playerCount = -1;
        currentTeam = -1;
    }

    void ProcessSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if (myPawn.currentX == enemyPawn.currentX)
            {
                if (
                    myPawn.currentY == enemyPawn.currentY - 1
                    || myPawn.currentY == enemyPawn.currentY + 1
                )
                {
                    if (enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                                - bounds
                                + new Vector3(tileSize / 2, 0, tileSize / 2)
                                + (Vector3.forward * deathSpacing) * deadWhites.Count
                        );
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                                - bounds
                                + new Vector3(tileSize / 2, 0, tileSize / 2)
                                + (Vector3.back * deathSpacing) * deadBlacks.Count
                        );
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == ChessPieceType.Pawn)
            {
                if (targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y]
                        .transform
                        .position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePieces(lastMove[1].x, lastMove[1].y);
                }
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y]
                        .transform
                        .position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePieces(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            if (lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePieces(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePieces(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePieces(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePieces(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }

    public void ProcessSpecialMoveSimulate(
        ref ChessPiece[,] chessPieces,
        SpecialMove specMove,
        ref List<Vector2Int[]> listMove
    )
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = listMove[listMove.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = listMove[listMove.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if (myPawn.currentX == enemyPawn.currentX)
            {
                if (
                    myPawn.currentY == enemyPawn.currentY - 1
                    || myPawn.currentY == enemyPawn.currentY + 1
                )
                {
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = listMove[listMove.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == ChessPieceType.Pawn)
            {
                if (targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = new ChessPiece();
                    newQueen.setProperties(1, lastMove[1].x, lastMove[1].y, ChessPieceType.Queen);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                }
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = new ChessPiece();
                    newQueen.setProperties(1, lastMove[1].x, lastMove[1].y, ChessPieceType.Queen);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                }
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = listMove[listMove.Count - 1];

            if (lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;

                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;

                    chessPieces[0, 7] = null;
                }
            }
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;

                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;

                    chessPieces[7, 7] = null;
                }
            }
        }
    }

    public void PreventCheck(
        ChessPiece[,] chessPieces,
        ref List<Vector2Int> moves,
        ChessPiece currentlyDragging,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    if (chessPieces[x, y].type == ChessPieceType.King)
                        if (chessPieces[x, y].team == currentlyDragging.team)
                            targetKing = chessPieces[x, y];
        SimulateMoveForSinglePiece(currentlyDragging, ref moves, targetKing);
    }

    void SimulateMoveForSinglePiece(
        ChessPiece cp,
        ref List<Vector2Int> moves,
        ChessPiece targetKing
    )
    {
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(
                targetKing.currentX,
                targetKing.currentY
            );

            if (cp.type == ChessPieceType.King)
                kingPositionThisSim = new Vector2Int(simX, simY);

            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();

            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simAttackingPieces.Add(simulation[x, y]);
                    }
                }
            }

            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            List<Vector2Int> simMoves = new List<Vector2Int>();

            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(
                    ref simulation,
                    TILE_COUNT_X,
                    TILE_COUNT_Y
                );
                for (int b = 0; b < pieceMoves.Count; b++)
                    simMoves.Add(pieceMoves[b]);
            }

            if (ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }

            cp.currentX = actualX;
            cp.currentY = actualY;
        }
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
    }

    bool CheckForCheckMate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }

        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(
                ref chessPieces,
                TILE_COUNT_X,
                TILE_COUNT_Y
            );
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }
        if (
            ContainsValidMove(
                ref currentAvailableMoves,
                new Vector2Int(targetKing.currentX, targetKing.currentY)
            )
        )
        {
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(
                    ref chessPieces,
                    TILE_COUNT_X,
                    TILE_COUNT_Y
                );
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);
                if (defendingMoves.Count != 0)
                    return false;
            }
            return true;
        }

        return false;
    }

    Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
        return -Vector2Int.one;
    }

    #region
    void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
        GameUI.Instance.SetMatchMinimaxGame += OnSetMatchMinimaxGame;
        GameUI.Instance.SetMatchReinforceGame += OnSetMatchReinforceGame;
    }

    void UnregisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
        GameUI.Instance.SetMatchMinimaxGame -= OnSetMatchMinimaxGame;
        GameUI.Instance.SetMatchReinforceGame -= OnSetMatchReinforceGame;
    }

    void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        NetWelcome nw = msg as NetWelcome;

        nw.AssignedTeam = ++playerCount;

        Server.Instance.SendToClient(cnn, nw);

        if (playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }

    void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        NetMakeMove nm = msg as NetMakeMove;
        Server.Instance.Broadcast(nm);
    }

    void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {
        NetRematch rm = msg as NetRematch;
        Server.Instance.Broadcast(rm);
    }

    void OnWelcomeClient(NetMessage msg)
    {
        NetWelcome nw = msg as NetWelcome;

        currentTeam = nw.AssignedTeam;

        Debug.Log($"My assigned team is {nw.AssignedTeam}");

        if (localGame && currentTeam == 0)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }

    void OnStartGameClient(NetMessage msg)
    {
        GameUI.Instance.ChangeCamera(
            (currentTeam == 0) ? CameraAngles.whiteTeam : CameraAngles.blackTeam
        );
    }

    void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove nm = msg as NetMakeMove;
        Debug.Log(
            $"MM: {nm.teamId} : {nm.originalX} {nm.originalY} -> {nm.destinationX} {nm.destinationY}"
        );
        if (!localGame)
            if (nm.teamId != currentTeam)
            {
                ChessPiece target = chessPieces[nm.originalX, nm.originalY];
                availableMoves = target.GetAvailableMoves(
                    ref chessPieces,
                    TILE_COUNT_X,
                    TILE_COUNT_Y
                );
                specialMove = target.GetSpecialMoves(
                    ref chessPieces,
                    ref moveList,
                    ref availableMoves
                );
                MoveTo(nm.originalX, nm.originalY, nm.destinationX, nm.destinationY);
                RemoveHighlightTiles();
            }
    }

    void OnRematchClient(NetMessage msg)
    {
        NetRematch rm = msg as NetRematch;
        playerRematch[rm.teamId] = (rm.wantRematch == 1);

        if (rm.teamId != currentTeam)
        {
            rematchIndicator.transform
                .GetChild((rm.wantRematch == 1) ? 0 : 1)
                .gameObject.SetActive(true);
            if (rm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }
        }

        if (playerRematch[0] && playerRematch[1])
        {
            GameReset();
        }
    }

    void OnSetLocalGame(bool v)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
    }

    void OnSetMatchMinimaxGame()
    {
        bot = new MinimaxBot();
        playerCount = -1;
        currentTeam = -1;
        localGame = true;
    }

    void OnSetMatchReinforceGame()
    {
        bot = new RandomBot();
        playerCount = -1;
        currentTeam = -1;
        localGame = true;
    }

    void ShutdownRelay()
    {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
        GameReset();
    }
    #endregion
}
