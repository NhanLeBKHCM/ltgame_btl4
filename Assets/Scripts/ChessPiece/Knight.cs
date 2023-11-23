using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;

public class Knight : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(
        ref ChessPiece[,] board,
        int tileCountX,
        int tileCountY
    )
    {
        List<Vector2Int> r = new List<Vector2Int>();
        int[] x_set = { -1, 1, 2, -2 };
        int x,
            y;
        for (int i = 0; i < x_set.Count(); i++)
        {
            x = currentX + x_set[i];
            y = 3 - Math.Abs(x_set[i]) + currentY;
            if (x < tileCountX && x >= 0 && y < tileCountX && y >= 0)
                if (board[x, y] == null || board[x, y].team != team)
                    r.Add(new Vector2Int(x, y));
            y = -(3 - Math.Abs(x_set[i])) + currentY;
            if (x < tileCountX && x >= 0 && y < tileCountX && y >= 0)
                if (board[x, y] == null || board[x, y].team != team)
                    r.Add(new Vector2Int(x, y));
        }

        return r;
    }
}
