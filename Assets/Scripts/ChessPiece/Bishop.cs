using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bishop : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(
        ref ChessPiece[,] board,
        int tileCountX,
        int tileCountY
    )
    {
        List<Vector2Int> r = new List<Vector2Int>();
        for (int i = 1; -i + currentX >= 0 && -i + currentY >= 0; i++)
        {
            if (board[currentX - i, currentY - i] == null)
                r.Add(new Vector2Int(currentX - i, currentY - i));
            if (board[currentX - i, currentY - i] != null)
            {
                if (board[currentX - i, currentY - i].team != team)
                    r.Add(new Vector2Int(currentX - i, currentY - i));
                break;
            }
        }
        for (int i = 1; i + currentX <= tileCountX - 1 && -i + currentY >= 0; i++)
        {
            if (board[currentX + i, currentY - i] == null)
                r.Add(new Vector2Int(currentX + i, currentY - i));
            if (board[currentX + i, currentY - i] != null)
            {
                if (board[currentX + i, currentY - i].team != team)
                    r.Add(new Vector2Int(currentX + i, currentY - i));
                break;
            }
        }
        for (int i = 1; i + currentX <= tileCountX - 1 && i + currentY <= tileCountY - 1; i++)
        {
            if (board[currentX + i, currentY + i] == null)
                r.Add(new Vector2Int(currentX + i, currentY + i));
            if (board[currentX + i, currentY + i] != null)
            {
                if (board[currentX + i, currentY + i].team != team)
                    r.Add(new Vector2Int(currentX + i, currentY + i));
                break;
            }
        }
        for (int i = 1; -i + currentX >= 0 && i + currentY <= tileCountY - 1; i++)
        {
            if (board[currentX - i, currentY + i] == null)
                r.Add(new Vector2Int(currentX - i, currentY + i));
            if (board[currentX - i, currentY + i] != null)
            {
                if (board[currentX - i, currentY + i].team != team)
                    r.Add(new Vector2Int(currentX - i, currentY + i));
                break;
            }
        }
        return r;
    }
}
