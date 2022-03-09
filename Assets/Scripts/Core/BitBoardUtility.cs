using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chess {
	public static class BitBoardUtility {
        /// <summary>
        /// 判断bitboard中是否包含某个格子序号
        /// </summary>
		public static bool ContainsSquare (ulong bitboard, int square) {
			return ((bitboard >> square) & 1) != 0; // bitboard右移square位之后，square对应格子如果有棋子，最低位是1
		}
	}
}