namespace Chess {

    // 棋盘标识
	public static class BoardRepresentation {
		public const string fileNames = "abcdefgh"; // 横坐标对应的标识组
		public const string rankNames = "12345678"; // 纵坐标对应的标识组

		public const int a1 = 0;
		public const int b1 = 1;
		public const int c1 = 2;
		public const int d1 = 3;
		public const int e1 = 4;
		public const int f1 = 5;
		public const int g1 = 6;
		public const int h1 = 7;

		public const int a8 = 56;
		public const int b8 = 57;
		public const int c8 = 58;
		public const int d8 = 59;
		public const int e8 = 60;
		public const int f8 = 61;
		public const int g8 = 62;
		public const int h8 = 63;

		// Rank (0 to 7) of square 获取当前方块序号的对应的纵坐标  比如12（001100）这个数对应的是e2标识，对应的纵坐标是1（000001）
		public static int RankIndex (int squareIndex) {
			return squareIndex >> 3; // 操作数每右移一位，相当于该数除以2。
		}

		// File (0 to 7) of square 获取当前方块序号的对应的横坐标  比如12（001100）这个数对应的是e2标识，对应的横坐标是4（000100）
		public static int FileIndex (int squareIndex) {
			return squareIndex & 0b000111;
		}

        // 从实际坐标获取方块序号
		public static int IndexFromCoord (int fileIndex, int rankIndex) {
			return rankIndex * 8 + fileIndex;
		}

        // 从方块对象获取方块序号
		public static int IndexFromCoord (Coord coord) {
			return IndexFromCoord (coord.fileIndex, coord.rankIndex);
		}

        // 从方块序号获取坐标对象
		public static Coord CoordFromIndex (int squareIndex) {
			return new Coord (FileIndex (squareIndex), RankIndex (squareIndex));
		}

        // 是否是白块
		public static bool LightSquare (int fileIndex, int rankIndex) {
			return (fileIndex + rankIndex) % 2 != 0;
		}

        // 从实际坐标获取方块名称
		public static string SquareNameFromCoordinate (int fileIndex, int rankIndex) {
			return fileNames[fileIndex] + "" + (rankIndex + 1);
		}

        // 从方块序号获取方块名称
		public static string SquareNameFromIndex (int squareIndex) {
			return SquareNameFromCoordinate (CoordFromIndex (squareIndex));
		}

        // 从坐标对象获取方块名称
		public static string SquareNameFromCoordinate (Coord coord) {
			return SquareNameFromCoordinate (coord.fileIndex, coord.rankIndex);
		}
	}
}