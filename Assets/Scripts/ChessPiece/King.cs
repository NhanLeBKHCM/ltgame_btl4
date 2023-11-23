using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

public class King : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(
        ref ChessPiece[,] board,
        int tileCountX,
        int tileCountY
    )
    {
        List<Vector2Int> r = new List<Vector2Int>();
        int[] increase_set = { -1, 0, 1 };
        for (int i = 0; i < increase_set.Count(); i++)
            for (int j = 0; j < increase_set.Count(); j++)
                if (
                    currentX + increase_set[i] >= 0
                    && currentX + increase_set[i] < tileCountX
                    && currentY + increase_set[j] >= 0
                    && currentY + increase_set[j] < tileCountY
                    && !(increase_set[i] == 0 && increase_set[j] == 0)
                )
                {
                    if (board[currentX + increase_set[i], currentY + increase_set[j]] == null)
                        r.Add(
                            new Vector2Int(currentX + increase_set[i], currentY + increase_set[j])
                        );
                    else if (
                        board[currentX + increase_set[i], currentY + increase_set[j]].team != team
                    )
                        r.Add(
                            new Vector2Int(currentX + increase_set[i], currentY + increase_set[j])
                        );
                }

        return r;
    }

    public override SpecialMove GetSpecialMoves(
        ref ChessPiece[,] board,
        ref List<Vector2Int[]> moveList,
        ref List<Vector2Int> availableMove
    )
    {
        SpecialMove r = SpecialMove.None;

        var kingMove = moveList.Find(m => m[0].x == 4 && m[0].y == ((team == 0) ? 0 : 7));
        var leftRook = moveList.Find(m => m[0].x == 0 && m[0].y == ((team == 0) ? 0 : 7));
        var rightRook = moveList.Find(m => m[0].x == 7 && m[0].y == ((team == 0) ? 0 : 7));

        if (kingMove == null && currentX == 4)
        {
            if (team == 0)
            {
                if (leftRook == null)
                    if (board[0, 0].type == ChessPieceType.Rook)
                        if (board[0, 0].team == 0)
                            if (board[3, 0] == null)
                                if (board[2, 0] == null)
                                    if (board[1, 0] == null)
                                    {
                                        availableMove.Add(new Vector2Int(2, 0));
                                        r = SpecialMove.Castling;
                                    }
                if (leftRook == null)
                    if (board[7, 0].type == ChessPieceType.Rook)
                        if (board[7, 0].team == 0)
                            if (board[5, 0] == null)
                                if (board[6, 0] == null)
                                {
                                    availableMove.Add(new Vector2Int(6, 0));
                                    r = SpecialMove.Castling;
                                }
            }
            else
            {
                if (leftRook == null)
                    if (board[0, 7].type == ChessPieceType.Rook)
                        if (board[0, 7].team == 1)
                            if (board[3, 7] == null)
                                if (board[2, 7] == null)
                                    if (board[1, 7] == null)
                                    {
                                        availableMove.Add(new Vector2Int(2, 7));
                                        r = SpecialMove.Castling;
                                    }
                if (leftRook == null)
                    if (board[7, 7].type == ChessPieceType.Rook)
                        if (board[7, 7].team == 1)
                            if (board[5, 7] == null)
                                if (board[6, 7] == null)
                                {
                                    availableMove.Add(new Vector2Int(6, 7));
                                    r = SpecialMove.Castling;
                                }
            }
        }

        return r;
    }
}
