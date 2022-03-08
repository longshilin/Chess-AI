namespace Chess {
    /// <summary>
    /// 棋盘的最小单元，区分每种棋子类型，以及提供方法去辨识
    /// </summary>
	public static class Piece {

		public const int None = 0;
        public const int King = 1; //国王          0001           “最为重要的棋子”，每次可以朝每个方向前进一格。王所走到的位置不可有对方棋子的威胁，否则会被视为“违规移动”。其吃子与走法相同。
        public const int Pawn = 2; //兵            0010           第一步可前进一格或两格，以后每次只能前进一格，不可后退。只能吃掉斜前方一格的棋子，并落在该格。
        public const int Knight = 3; //马（骑士）   0011           “唯一能够越子的棋子”，呈“日”字型或“L”字型。其吃子与走法相同。
        public const int Bishop = 5; //相（教皇）   0101           只可斜走，格数不限，但不可转向或越过其他棋子。因此白格象只能在白格走动，黑格象只能在黑格走动。每局开始时，每一方有双象，一在黑格，一在白格。
        public const int Rook = 6; //车（城堡）     0110           横走或直走，格数不限，但不可斜走或越过其他棋子。其吃子与走法相同。
        public const int Queen = 7; //皇后         0111           “威力最强的棋子”，可横走、直走或斜走，移动步数不限，但不可转向或越过其他棋子。其吃子与走法相同。


        public const int White = 8; //白子         01000
        public const int Black = 16; //黑子        10000

        const int typeMask = 0b00111; //棋子类型占位符(0~7 二进制的低三位可以表示)
        const int blackMask = 0b10000; //黑子类型占位符
        const int whiteMask = 0b01000; //白子类型占位符
        const int colourMask = whiteMask | blackMask; //颜色类型占位符 0b11000

        /// <summary>
        /// 是否是同色棋子
        /// </summary>
		public static bool IsColour (int piece, int colour) {
			return (piece & colourMask) == colour;
		}

        /// <summary>
        /// 查看棋子颜色
        /// </summary>
		public static int Colour (int piece) {
			return piece & colourMask;
		}

        /// <summary>
        /// 查看棋子类型
        /// </summary>
		public static int PieceType (int piece) {
			return piece & typeMask;
		}

        /// <summary>
        /// 车或者皇后
        /// </summary>
		public static bool IsRookOrQueen (int piece) {
			return (piece & 0b110) == 0b110;
		}

        /// <summary>
        /// 相或皇后
        /// </summary>
		public static bool IsBishopOrQueen (int piece) {
			return (piece & 0b101) == 0b101;
		}

        /// <summary>
        /// 是否可以滑行，相 车 皇后 移动步数不限
        /// </summary>
		public static bool IsSlidingPiece (int piece) {
			return (piece & 0b100) != 0;
		}
	}
}