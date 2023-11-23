using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class Bot
{
    public virtual (Vector2Int, Vector2Int) GetStep(
        int team,
        ChessPiece[,] simulation,
        int TILE_COUNT_X,
        int TILE_COUNT_Y
    )
    {
        return (new Vector2Int(), new Vector2Int());
    }
}

public class RandomBot : Bot
{
    public override (Vector2Int, Vector2Int) GetStep(
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
        Random rnd = new Random();
        int idx_rnd = rnd.Next(availableSteps.Count);
        return availableSteps[idx_rnd];
    }
}
