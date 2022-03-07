using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chess.Game {
	public class HumanPlayer : Player {

        // 玩家的走棋方式
		public enum InputState {
			None,
			PieceSelected, // 选择棋子
			DraggingPiece // 拖动棋子
		}

		InputState currentState;

		BoardUI boardUI;
		Camera cam;
		Coord selectedPieceSquare;
		Board board;
		public HumanPlayer (Board board) {
			boardUI = GameObject.FindObjectOfType<BoardUI> ();
			cam = Camera.main;
			this.board = board;
		}

		public override void NotifyTurnToMove () {

		}

		public override void Update () {
			HandleInput ();
		}

        /// <summary>
        /// 处理玩家UI点击输入，默认情况下currentState是None状态
        /// </summary>
		void HandleInput () {
			Vector2 mousePos = cam.ScreenToWorldPoint (Input.mousePosition);
			if (currentState == InputState.None) {
                // 1-棋子被点击时最先会执行这个 ，currentState=DraggingPiece
				HandlePieceSelection (mousePos);
			} else if (currentState == InputState.DraggingPiece) {
                // 2-然后currentState会转到这个状态，如果此时送开鼠标左键则currentState=PieceSelected，否则一直处于拖动棋子状态
				HandleDragMovement (mousePos);
			} else if (currentState == InputState.PieceSelected) {
                // 3-棋子落子时最终会到这个状态
				HandlePointAndClickMovement (mousePos);
			}

            // 右键撤销选中状态
			if (Input.GetMouseButtonDown (1)) {
				CancelPieceSelection ();
			}
		}

        /// <summary>
        /// 棋子被单机选中
        /// </summary>
        /// <param name="mousePos"></param>
		void HandlePointAndClickMovement (Vector2 mousePos) {
			if (Input.GetMouseButton (0)) {
				HandlePiecePlacement (mousePos);
			}
		}

        /// <summary>
        /// 拖动棋子时的状态
        /// </summary>
        /// <param name="mousePos"></param>
		void HandleDragMovement (Vector2 mousePos) {
			boardUI.DragPiece (selectedPieceSquare, mousePos); // 鼠标不松开之前，棋子随鼠标位置更新
			// If mouse is released, then try place the piece 鼠标左键松开时处理落子逻辑
			if (Input.GetMouseButtonUp (0)) {
				HandlePiecePlacement (mousePos);
			}
		}

        /// <summary>
        /// 棋子被鼠标左键松开时，则处理棋子落子的逻辑
        /// </summary>
        void HandlePiecePlacement (Vector2 mousePos) {
			Coord targetSquare;
			if (boardUI.TryGetSquareUnderMouse (mousePos, out targetSquare)) {
                if (targetSquare.Equals (selectedPieceSquare)) { // 判断当前选中的棋子坐标对象和之前的是否是同一个，如果selectedPieceSquare本来为null，则返回false
					boardUI.ResetPiecePosition (selectedPieceSquare);
                    // 点击棋子未松开前处于DraggingPiece，松开了处于PieceSelected
					if (currentState == InputState.DraggingPiece) {
						currentState = InputState.PieceSelected;
					} else {
                        // 再次点击同一个棋子 重置状态
						currentState = InputState.None;
						boardUI.DeselectSquare (selectedPieceSquare);
					}
				} else {
                    // 选择和上次不是同一个棋子对象时
					int targetIndex = BoardRepresentation.IndexFromCoord (targetSquare.fileIndex, targetSquare.rankIndex);
                    // 判断这次选中的是否是同色棋子
					if (Piece.IsColour (board.Square[targetIndex], board.ColourToMove) && board.Square[targetIndex] != 0) {
                        // 取消上次的选中，并执行这次选择逻辑
						CancelPieceSelection ();
						HandlePieceSelection (mousePos);
					} else {
                        // 如果不是点击我方的其他棋子，则尝试去走棋
						TryMakeMove (selectedPieceSquare, targetSquare); // 第一个参数是选中的棋子，第二个参数是需要走到的地方
					}
				}
			} else {
				CancelPieceSelection ();
			}

		}

		void CancelPieceSelection () {
			if (currentState != InputState.None) {
				currentState = InputState.None;
				boardUI.DeselectSquare (selectedPieceSquare);
				boardUI.ResetPiecePosition (selectedPieceSquare);
			}
		}

        /// <summary>
        /// 尝试走棋，判断走棋是否有效
        /// </summary>
        void TryMakeMove (Coord startSquare, Coord targetSquare) {
			int startIndex = BoardRepresentation.IndexFromCoord (startSquare);
			int targetIndex = BoardRepresentation.IndexFromCoord (targetSquare);
			bool moveIsLegal = false;
			Move chosenMove = new Move ();

			MoveGenerator moveGenerator = new MoveGenerator ();
			bool wantsKnightPromotion = Input.GetKey (KeyCode.LeftAlt);

			var legalMoves = moveGenerator.GenerateMoves (board);
			for (int i = 0; i < legalMoves.Count; i++) {
				var legalMove = legalMoves[i];

				if (legalMove.StartSquare == startIndex && legalMove.TargetSquare == targetIndex) {
					if (legalMove.IsPromotion) {
						if (legalMove.MoveFlag == Move.Flag.PromoteToQueen && wantsKnightPromotion) {
							continue;
						}
						if (legalMove.MoveFlag != Move.Flag.PromoteToQueen && !wantsKnightPromotion) {
							continue;
						}
					}
					moveIsLegal = true;
					chosenMove = legalMove;
					//	Debug.Log (legalMove.PromotionPieceType);
					break;
				}
			}

			if (moveIsLegal) {
                // 棋子走动合法
				ChoseMove (chosenMove);
				currentState = InputState.None;
			} else {
                // 棋子走动非法时，取消该棋子选中
				CancelPieceSelection ();
			}
		}

        // 棋子被选中时首先会执行这个，并进入currentState是DraggingPiece状态
		void HandlePieceSelection (Vector2 mousePos) {
			if (Input.GetMouseButtonDown (0)) {
				if (boardUI.TryGetSquareUnderMouse (mousePos, out selectedPieceSquare)) {
					int index = BoardRepresentation.IndexFromCoord (selectedPieceSquare);
					// If square contains a piece, select that piece for dragging
					if (Piece.IsColour (board.Square[index], board.ColourToMove)) {
						boardUI.HighlightLegalMoves (board, selectedPieceSquare);
						boardUI.SelectSquare (selectedPieceSquare);
						currentState = InputState.DraggingPiece;
					}
				}
			}
		}
	}
}