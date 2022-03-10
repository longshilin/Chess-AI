namespace Chess {
	using System.Collections.Generic;
	using static System.Math;

    // 预先计算的移动相关数据
	public static class PrecomputedMoveData {
		// First 4 are orthogonal, last 4 are diagonals (N, S, W, E, NW, SE, NE, SW) 九宫格除去中间格后外围八个格子的Index，中间格子的Index为0
        // https://cdn.jsdelivr.net/gh/longshilin/images/%E7%8E%8B%E5%8F%AF%E4%BB%A5%E8%B5%B0%E7%9A%84%E5%8C%BA%E5%9F%9F.png
        //  7  8  9
        // -1 [0] 1
        // -9 -8 -7
		public static readonly int[] directionOffsets = { 8, -8, -1, 1, 7, -7, 9, -9 };
        
		// Stores number of moves available in each of the 8 directions for every square on the board  存储棋盘上每个方块的 8 个方向可移动的格子数
		// Order of directions is: N, S, W, E, NW, SE, NE, SW
		// So for example, if availableSquares[0][1] == 7...
		// that means that there are 7 squares to the north of b1 (the square with index 1 in board array) b1这个棋子在北这个方向可以走动的格子数是7个格子
		public static readonly int[][] numSquaresToEdge; // 第一维是格子Index序号，第二维是八个方向的序号，对应的值是沿着这个方向能行进的格子数

		// Stores array of indices for each square a knight can land on from any square on the board  存储某个Index的棋子可以行进的位置
		// So for example, knightMoves[0] is equal to {10, 17}, meaning a knight on a1 can jump to c2 and b3  例如在Index为0(a1)的马，可以移动到Index为10(c2)和17(b3)的位置
		public static readonly byte[][] knightMoves; // 一维是马所在的位置序号，二维是在该位置可以走的点的序号数组
		public static readonly byte[][] kingMoves; // 一维是王所在的位置序号，二维是在该位置可以走的点的序号数组（0~7，共八个方向），对应的值是王在该点时对应可以走的八个方向的Index

		// Pawn attack directions for white and black (NW, NE; SW SE)  兵攻击目录
		public static readonly byte[][] pawnAttackDirections = {
			new byte[] { 4, 6 },
			new byte[] { 7, 5 }
		};

		public static readonly int[][] pawnAttacksWhite; // 兵攻击白子
		public static readonly int[][] pawnAttacksBlack; // 兵攻击黑子
		public static readonly int[] directionLookup; // 方向查找

		public static readonly ulong[] kingAttackBitboards; // 存储king从任意点可走动的位置，一个元素一个Index位置，对应的值是该点可以走的所有位置标记（ulong可以表示64位）
		public static readonly ulong[] knightAttackBitboards; // 存储knight从任意点可走动的位置
		public static readonly ulong[][] pawnAttackBitboards; // 兵可以走动的位置，第二维是黑白子区分

		public static readonly ulong[] rookMoves; // 车可走动的位置
		public static readonly ulong[] bishopMoves; // 相可走动的位置
		public static readonly ulong[] queenMoves; // 皇后可走动的位置

		// Aka manhattan distance (answers how many moves for a rook to get from square a to square b)  曼哈顿距离 计算车从方块a到方块b需要移动格子数
		public static int[, ] orthogonalDistance;
		// Aka chebyshev distance (answers how many moves for a king to get from square a to square b)  切比雪夫距离 计算国王从方块a到方块b需要移动格子数
		public static int[, ] kingDistance;
		public static int[] centreManhattanDistance; // 距离棋盘中点（3，4）的曼哈顿距离

		public static int NumRookMovesToReachSquare (int startSquare, int targetSquare) {
			return orthogonalDistance[startSquare, targetSquare];
		}

		public static int NumKingMovesToReachSquare (int startSquare, int targetSquare) {
			return kingDistance[startSquare, targetSquare];
		}

		// Initialize lookup data
		static PrecomputedMoveData () {
			pawnAttacksWhite = new int[64][];
			pawnAttacksBlack = new int[64][];
			numSquaresToEdge = new int[8][];
			knightMoves = new byte[64][];
			kingMoves = new byte[64][];
			numSquaresToEdge = new int[64][];

			rookMoves = new ulong[64];
			bishopMoves = new ulong[64];
			queenMoves = new ulong[64];

			// Calculate knight jumps and available squares for each square on the board.  计算骑士可跳跃的棋盘格子序号。
			// See comments by variable definitions for more info.
            // https://cdn.jsdelivr.net/gh/longshilin/images/%E9%A9%AC%E5%8F%AF%E4%BB%A5%E8%A1%8C%E8%B5%B0%E7%9A%84%E5%8C%BA%E5%9F%9F.png
			int[] allKnightJumps = { 15, 17, -17, -15, 10, -6, 6, -10 };
			knightAttackBitboards = new ulong[64];
			kingAttackBitboards = new ulong[64];
			pawnAttackBitboards = new ulong[64][];

			for (int squareIndex = 0; squareIndex < 64; squareIndex++) {

                // 获取格子序号对应的格子坐标 比如序号8对应的坐标是(0,1)
				int y = squareIndex / 8;
				int x = squareIndex - y * 8;

                // 记录格子在八个方向上到达棋盘边界所需的步数
				int north = 7 - y;
				int south = y;
				int west = x;
				int east = 7 - x;
				numSquaresToEdge[squareIndex] = new int[8];
				numSquaresToEdge[squareIndex][0] = north; // 当前格子距离北面可行走的格子数
				numSquaresToEdge[squareIndex][1] = south; // 当前格子距离南面可行走的格子数
				numSquaresToEdge[squareIndex][2] = west; // 当前格子距离西面可行走的格子数
				numSquaresToEdge[squareIndex][3] = east; // 当前格子距离东面可行走的格子数
				numSquaresToEdge[squareIndex][4] = System.Math.Min (north, west); // 当前格子距离西北斜向可行走的格子数
				numSquaresToEdge[squareIndex][5] = System.Math.Min (south, east); // 当前格子距离东南斜向面可行走的格子数
				numSquaresToEdge[squareIndex][6] = System.Math.Min (north, east); // 当前格子距离东北斜向面可行走的格子数
				numSquaresToEdge[squareIndex][7] = System.Math.Min (south, west); // 当前格子距离东南斜向面可行走的格子数

				// Calculate all squares knight can jump to from current square 计算马在棋盘格子任意格子可以走的位置
				var legalKnightJumps = new List<byte> ();
				ulong knightBitboard = 0;
				foreach (int knightJumpDelta in allKnightJumps) {
					int knightJumpSquare = squareIndex + knightJumpDelta;
					if (knightJumpSquare >= 0 && knightJumpSquare < 64) {
						int knightSquareY = knightJumpSquare / 8;
						int knightSquareX = knightJumpSquare - knightSquareY * 8;
						// Ensure knight has moved max of 2 squares on x/y axis (to reject indices that have wrapped around side of board)
						int maxCoordMoveDst = System.Math.Max (System.Math.Abs (x - knightSquareX), System.Math.Abs (y - knightSquareY));
						if (maxCoordMoveDst == 2) {
							legalKnightJumps.Add ((byte) knightJumpSquare); // 如果马在Index为0这个点，那么可以走的位置是17和10
							knightBitboard |= 1ul << knightJumpSquare; // 无符号长整型最长可表示64位，每个可走的点可标记一位，当前knightBitboard也就是 2的17次方+2的10次方的和132096
						}
					}
				}
				knightMoves[squareIndex] = legalKnightJumps.ToArray ();
				knightAttackBitboards[squareIndex] = knightBitboard;

				// Calculate all squares king can move to from current square (not including castling)  计算国王可以从当前方格移动到的所有方格
				var legalKingMoves = new List<byte> ();
				foreach (int kingMoveDelta in directionOffsets) {
					int kingMoveSquare = squareIndex + kingMoveDelta;
					if (kingMoveSquare >= 0 && kingMoveSquare < 64) {
						int kingSquareY = kingMoveSquare / 8;
						int kingSquareX = kingMoveSquare - kingSquareY * 8;
						// Ensure king has moved max of 1 square on x/y axis (to reject indices that have wrapped around side of board)
						int maxCoordMoveDst = System.Math.Max (System.Math.Abs (x - kingSquareX), System.Math.Abs (y - kingSquareY));
						if (maxCoordMoveDst == 1) { // 计算所有和国王棋子距离等于1的格子
							legalKingMoves.Add ((byte) kingMoveSquare);
							kingAttackBitboards[squareIndex] |= 1ul << kingMoveSquare;
						}
					}
				}
				kingMoves[squareIndex] = legalKingMoves.ToArray (); // 记录王所在格子对应可以走的所有点位

				// Calculate legal pawn captures for white and black  计算白方和黑方兵能移动的位置
				// https://cdn.jsdelivr.net/gh/longshilin/images/%E5%85%B5%E8%83%BD%E8%B5%B0%E7%9A%84%E4%BD%8D%E7%BD%AE%EF%BC%88%E5%88%86%E9%BB%91%E7%99%BD%E5%85%B5%EF%BC%89.png
                //  7  8  9
                // -1 [0] 1
                // -7 -8 -9 
                // Index为0的白子往N方向进攻时，可以吃7和9位置；Index为0的黑子往S方向进攻时，可以吃-7和-9位置
				List<int> pawnCapturesWhite = new List<int> ();
				List<int> pawnCapturesBlack = new List<int> ();
				pawnAttackBitboards[squareIndex] = new ulong[2];
				if (x > 0) {
					if (y < 7) {
						pawnCapturesWhite.Add (squareIndex + 7);
						pawnAttackBitboards[squareIndex][Board.WhiteIndex] |= 1ul << (squareIndex + 7);
					}
					if (y > 0) {
						pawnCapturesBlack.Add (squareIndex - 9);
						pawnAttackBitboards[squareIndex][Board.BlackIndex] |= 1ul << (squareIndex - 9);
					}
				}
				if (x < 7) {
					if (y < 7) {
						pawnCapturesWhite.Add (squareIndex + 9);
						pawnAttackBitboards[squareIndex][Board.WhiteIndex] |= 1ul << (squareIndex + 9);
					}
					if (y > 0) {
						pawnCapturesBlack.Add (squareIndex - 7);
						pawnAttackBitboards[squareIndex][Board.BlackIndex] |= 1ul << (squareIndex - 7);
					}
				}
				pawnAttacksWhite[squareIndex] = pawnCapturesWhite.ToArray (); // 每个格子白子能走的位置
				pawnAttacksBlack[squareIndex] = pawnCapturesBlack.ToArray (); // 每个格子黑子能走的位置

				// Rook moves 车能移动的位点，0~3前四个正方向纵深到底
				for (int directionIndex = 0; directionIndex < 4; directionIndex++) {
					int currentDirOffset = directionOffsets[directionIndex]; // 当前选取的方向
					for (int n = 0; n < numSquaresToEdge[squareIndex][directionIndex]; n++) {
                        // 当前方向上纵深遍历
						int targetSquare = squareIndex + currentDirOffset * (n + 1); // 格子地图序号 + 选取方向的单位间隔 * 纵深层数
						rookMoves[squareIndex] |= 1ul << targetSquare;
					}
				}
				// Bishop moves 相能移动的位点，4~7后四个斜向方向纵深到底
				for (int directionIndex = 4; directionIndex < 8; directionIndex++) {
					int currentDirOffset = directionOffsets[directionIndex];
					for (int n = 0; n < numSquaresToEdge[squareIndex][directionIndex]; n++) {
						int targetSquare = squareIndex + currentDirOffset * (n + 1);
						bishopMoves[squareIndex] |= 1ul << targetSquare;
					}
				}

                // Queen moves 皇后可以移动的位置就是车和相走棋位点的或
				queenMoves[squareIndex] = rookMoves[squareIndex] | bishopMoves[squareIndex];
			}

            // todo 这个是干嘛用的？ 移动方向？  左侧对角是-9/9，右侧对角是-7/7，右侧纵轴是8
			directionLookup = new int[127];
			for (int i = 0; i < 127; i++) {
				int offset = i - 63;
				int absOffset = System.Math.Abs (offset);
				int absDir = 1;
				if (absOffset % 9 == 0) {
					absDir = 9;
				} else if (absOffset % 8 == 0) {
					absDir = 8;
				} else if (absOffset % 7 == 0) {
					absDir = 7;
				}

				directionLookup[i] = absDir * System.Math.Sign (offset);
			}

			// Distance lookup
			orthogonalDistance = new int[64, 64];
			kingDistance = new int[64, 64];
			centreManhattanDistance = new int[64];
			for (int squareA = 0; squareA < 64; squareA++) {
				Coord coordA = BoardRepresentation.CoordFromIndex (squareA);
				int fileDstFromCentre = Max (3 - coordA.fileIndex, coordA.fileIndex - 4);
				int rankDstFromCentre = Max (3 - coordA.rankIndex, coordA.rankIndex - 4);
				centreManhattanDistance[squareA] = fileDstFromCentre + rankDstFromCentre;

				for (int squareB = 0; squareB < 64; squareB++) {

					Coord coordB = BoardRepresentation.CoordFromIndex (squareB);
					int rankDistance = Abs (coordA.rankIndex - coordB.rankIndex);
					int fileDistance = Abs (coordA.fileIndex - coordB.fileIndex);
					orthogonalDistance[squareA, squareB] = fileDistance + rankDistance;
					kingDistance[squareA, squareB] = Max (fileDistance, rankDistance);
				}
			}
		}
	}
}