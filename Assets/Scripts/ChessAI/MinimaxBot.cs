using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class MinimaxBot : Bot
{
    private int GetPieceValue(ChessPiece cp)
    {
        if (cp == null)
            return 0;

        int v;
        switch (cp.type)
        {
            case ChessPieceType.None:
                v = 0;
                break;
            case ChessPieceType.Pawn:
                v = 10;
                break;
            case ChessPieceType.Bishop:
                v = 30;
                break;
            case ChessPieceType.Knight:
                v = 30;
                break;
            case ChessPieceType.Rook:
                v = 50;
                break;
            case ChessPieceType.Queen:
                v = 90;
                break;
            case ChessPieceType.King:
                v = 900;
                break;
            default:
                v = 0;
                break;
        }
        v = (cp.team == 0) ? v : -v;
        return v;
    }

    private int EvaluateBoard(ChessPiece[,] board, int TILE_COUNT_X, int TILE_COUNT_Y)
    {
        int v = 0;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                v += GetPieceValue(board[x, y]);
            }
        return v;
    }

    private List<(Vector2Int, Vector2Int)> GetAvailableSteps(
        int team,
        ChessPiece[,] simulation,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        List<(Vector2Int, Vector2Int)> availableSteps = new List<(Vector2Int, Vector2Int)>();
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (simulation[x, y] != null)
                    if (simulation[x, y].team == team)
                    {
                        ChessPiece cp = simulation[x, y];
                        List<Vector2Int> cp_moves = cp.GetAvailableMoves(
                            ref simulation,
                            TILE_COUNT_X,
                            TILE_COUNT_Y
                        );
                        Chessboard.Instance.PreventCheck(
                            simulation,
                            ref cp_moves,
                            cp,
                            TILE_COUNT_X,
                            TILE_COUNT_Y
                        );
                        for (int i = 0; i < cp_moves.Count; i++)
                            availableSteps.Add(
                                (new Vector2Int(cp.currentX, cp.currentY), cp_moves[i])
                            );
                    }
            }
        }

        return availableSteps;
    }

    private void copyBoard(
        ref ChessPiece[,] src,
        ref ChessPiece[,] des,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (src[x, y] == null)
                    des[x, y] = null;
                else
                {
                    if (des[x, y] != null)
                        Chessboard.Destroy(des[x, y].gameObject);
                    des[x, y] = Chessboard.Instance.SpawnSinglePiece(
                        src[x, y].type,
                        src[x, y].team,
                        false
                    );
                    des[x, y].setProperties(
                        src[x, y].team,
                        src[x, y].currentX,
                        src[x, y].currentY,
                        src[x, y].type
                    );
                }
            }
    }

    private int minimax(
        int depth,
        ChessPiece[,] board,
        bool isMaximisingPlayer,
        int TILE_COUNT_X,
        int TILE_COUNT_Y,
        int nodeValueParent
    )
    {
        int nodeValue;
        if (isMaximisingPlayer)
            nodeValue = -99999;
        else
            nodeValue = 99999;
        if (depth == 0)
        {
            return EvaluateBoard(board, TILE_COUNT_X, TILE_COUNT_Y);
        }

        List<(Vector2Int, Vector2Int)> availableSteps = GetAvailableSteps(
            isMaximisingPlayer ? 0 : 1,
            board,
            TILE_COUNT_X,
            TILE_COUNT_Y
        );
        if (availableSteps.Count == 0)
        {
            nodeValue = EvaluateBoard(board, TILE_COUNT_X, TILE_COUNT_Y);
            nodeValue -= (isMaximisingPlayer ? 900 : -900);
            return nodeValue;
        }
        ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        copyBoard(ref board, ref simulation, TILE_COUNT_X, TILE_COUNT_Y);

        for (int i = 0; i < availableSteps.Count; i++)
        {
            MakeMove(ref simulation, availableSteps[i], TILE_COUNT_X, TILE_COUNT_Y);
            int curValue = minimax(
                depth - 1,
                simulation,
                !isMaximisingPlayer,
                TILE_COUNT_X,
                TILE_COUNT_Y,
                nodeValue
            );
            if (i == 0)
                nodeValue = curValue;
            else
            {
                if (isMaximisingPlayer)
                    nodeValue = (nodeValue > curValue) ? nodeValue : curValue;
                else
                    nodeValue = (nodeValue < curValue) ? nodeValue : curValue;
            }
            if (isMaximisingPlayer && (nodeValue > nodeValueParent))
                return nodeValue;
            else if (!isMaximisingPlayer && (nodeValue < nodeValueParent))
                return nodeValue;
            copyBoard(ref board, ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
        }
        return nodeValue;
    }

    private bool MakeMove(
        ref ChessPiece[,] board,
        (Vector2Int, Vector2Int) step,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        if (board[step.Item1.x, step.Item1.y] == null)
            return false;
        board[step.Item2.x, step.Item2.y] = board[step.Item1.x, step.Item1.y];
        board[step.Item2.x, step.Item2.y].currentX = step.Item2.x;
        board[step.Item2.x, step.Item2.y].currentY = step.Item2.y;
        board[step.Item1.x, step.Item1.y] = null;
        return true;
    }

    public override (Vector2Int, Vector2Int) GetStep(
        int team,
        ChessPiece[,] board,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        List<(Vector2Int, Vector2Int)> availableSteps = GetAvailableSteps(
            team,
            board,
            TILE_COUNT_X,
            TILE_COUNT_Y
        );
        ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        copyBoard(ref board, ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
        int nodeValue = 999999;
        int depth = 2;
        int bestStep_idx = 0;
        for (int i = 0; i < availableSteps.Count; i++)
        {
            MakeMove(ref simulation, availableSteps[i], TILE_COUNT_X, TILE_COUNT_Y);
            int curValue = minimax(
                depth - 1,
                simulation,
                !((team == 0) ? true : false),
                TILE_COUNT_X,
                TILE_COUNT_Y,
                nodeValue
            );
            if (i == 0)
                nodeValue = curValue;
            else
            {
                if (nodeValue > curValue)
                {
                    nodeValue = curValue;
                    bestStep_idx = i;
                }
            }
            copyBoard(ref board, ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
        }

        Debug.Log(
            $"({availableSteps[bestStep_idx].Item1.x},{availableSteps[bestStep_idx].Item1.y})->({availableSteps[bestStep_idx].Item2.x},{availableSteps[bestStep_idx].Item2.y}) : value:{nodeValue}"
        );
        return availableSteps[bestStep_idx];
    }
}
