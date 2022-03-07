using UnityEngine;
namespace Chess.Game {
    /// <summary>
    /// 棋盘风格
    /// </summary>
	[CreateAssetMenu (menuName = "Theme/Board")]
	public class BoardTheme : ScriptableObject {

		public SquareColours lightSquares;
		public SquareColours darkSquares;

		[System.Serializable]
		public struct SquareColours {
            public Color normal; //正常idle区域
            public Color legal; //选中的棋子可移动的区域
            public Color selected; //选中棋子的区域
            public Color moveFromHighlight; //棋子移动前的区域
            public Color moveToHighlight; //棋子移动到的区域
		}
	}
}