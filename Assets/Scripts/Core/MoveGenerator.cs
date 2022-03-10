namespace Chess {
	using System.Collections.Generic;
	using static PrecomputedMoveData;
	using static BoardRepresentation;

	public class MoveGenerator {

		public enum PromotionMode { All, QueenOnly, QueenAndKnight }

		public PromotionMode promotionsToGenerate = PromotionMode.All;

		// ---- Instance variables ----
		List<Move> moves; // 每次Generator时格子对应的可能走棋
		bool isWhiteToMove;
		int friendlyColour; // 我方颜色
		int opponentColour; // 对方颜色
		int friendlyKingSquare; // 我方王的棋子Index
		int friendlyColourIndex; // 我方棋子颜色Index
		int opponentColourIndex; // 对方棋子颜色Index

		bool inCheck;
		bool inDoubleCheck; // 已经处理被将军的情况
		bool pinsExistInPosition;
		ulong checkRayBitmask;
		ulong pinRayBitmask;
		ulong opponentKnightAttacks;
		ulong opponentAttackMapNoPawns;
		public ulong opponentAttackMap; // 对方能攻击到的所有格子点位
		public ulong opponentPawnAttackMap; // 对方能攻击到的所有格子点位（不包含兵的攻击）
		ulong opponentSlidingAttackMap; // 对方滑行单位能攻击到的所有格子点位

		bool genQuiets; // 静止搜索,一种规避风险的附加搜索
		Board board;

		// Generates list of legal moves in current position.
		// Quiet moves (non captures) can optionally be excluded. This is used in quiescence search.   https://en.wikipedia.org/wiki/Quiescence_search  http://satirist.org/learn-game/methods/search/quiesce.html
        // 走棋路径生成,
        // 静止搜索是从主搜索的叶节点开始的附加搜索，它试图解决这个问题。在国际象棋中，静止搜索通常包括所有吃子动作，以便战术交换不会扰乱评估。原则上，静止搜索应包括可能破坏评估功能的任何移动。如果存在此类移动，则位置不是静止的。
		public List<Move> GenerateMoves (Board board, bool includeQuietMoves = true) {
			this.board = board;
			genQuiets = includeQuietMoves;
			Init (); // 生成器参数初始化

			CalculateAttackData (); // 计算对方棋子可以攻击到的所有位点数据，存在opponentAttackMap中 
			GenerateKingMoves (); // 生成王可选走棋路径

			// Only king moves are valid in a double check position, so can return early. 我方王被将军，只能移动王的位置，因此可以直接返回了
			if (inDoubleCheck) {
				return moves;
			}

			GenerateSlidingMoves (); // 生成可滑行棋子（车，相和皇后）的可选走棋路径
			GenerateKnightMoves (); // 生成马的可选走棋路径
			GeneratePawnMoves (); // 生成兵的可选走棋路径

			return moves;
		}

		// Note, this will only return correct value after GenerateMoves() has been called in the current position
		public bool InCheck () {
			return inCheck;
		}

		void Init () {
			moves = new List<Move> (64);
			inCheck = false;
			inDoubleCheck = false;
			pinsExistInPosition = false;
			checkRayBitmask = 0;
			pinRayBitmask = 0;

			isWhiteToMove = board.ColourToMove == Piece.White;
			friendlyColour = board.ColourToMove;
			opponentColour = board.OpponentColour;
			friendlyKingSquare = board.KingSquare[board.ColourToMoveIndex];
			friendlyColourIndex = (board.WhiteToMove) ? Board.WhiteIndex : Board.BlackIndex;
			opponentColourIndex = 1 - friendlyColourIndex;
		}

		void GenerateKingMoves () {
			for (int i = 0; i < kingMoves[friendlyKingSquare].Length; i++) { // 遍历王在friendlyKingSquare这个格子所能走的所有格子
				int targetSquare = kingMoves[friendlyKingSquare][i];
				int pieceOnTargetSquare = board.Square[targetSquare];

				// Skip squares occupied by friendly pieces 如果这个格子被自己的棋子占据，则跳过
				if (Piece.IsColour (pieceOnTargetSquare, friendlyColour)) {
					continue;
				}

				bool isCapture = Piece.IsColour (pieceOnTargetSquare, opponentColour); // 王所走的格子中有对方的棋子
				if (!isCapture) {
                    // 虽然王能吃这个棋子，但是吃这个棋子所在的格子处于对方的控制下，则不能走这一步，因为走过去就会被对方另外的棋子check，这是不被允许的。
					// King can't move to square marked as under enemy control, unless he is capturing that piece
					// Also skip if not generating quiet moves
					if (!genQuiets || SquareIsInCheckRay (targetSquare)) {
						continue;
					}
				}

				// Safe for king to move to this square 接下来王遍历能走的格子都是安全的
				if (!SquareIsAttacked (targetSquare)) {
					moves.Add (new Move (friendlyKingSquare, targetSquare));

					// Castling:  王自身不处于被check状态时，可以进行王车易位
					if (!inCheck && !isCapture) {
						// Castle kingside 在王翼侧进行王车易位
						if ((targetSquare == f1 || targetSquare == f8) && HasKingsideCastleRight) {
							int castleKingsideSquare = targetSquare + 1;
							if (board.Square[castleKingsideSquare] == Piece.None) {
								if (!SquareIsAttacked (castleKingsideSquare)) {
									moves.Add (new Move (friendlyKingSquare, castleKingsideSquare, Move.Flag.Castling));
								}
							}
						}
						// Castle queenside 在后翼侧进行王车易位
						else if ((targetSquare == d1 || targetSquare == d8) && HasQueensideCastleRight) {
							int castleQueensideSquare = targetSquare - 1;
							if (board.Square[castleQueensideSquare] == Piece.None && board.Square[castleQueensideSquare - 1] == Piece.None) {
								if (!SquareIsAttacked (castleQueensideSquare)) {
									moves.Add (new Move (friendlyKingSquare, castleQueensideSquare, Move.Flag.Castling));
								}
							}
						}
					}
				}
			}
		}

        /// <summary>
        /// 生成滑行棋子(车，马和皇后)的移动
        /// </summary>
		void GenerateSlidingMoves () {
			PieceList rooks = board.rooks[friendlyColourIndex];
			for (int i = 0; i < rooks.Count; i++) {
				GenerateSlidingPieceMoves (rooks[i], 0, 4);
			}

			PieceList bishops = board.bishops[friendlyColourIndex];
			for (int i = 0; i < bishops.Count; i++) {
				GenerateSlidingPieceMoves (bishops[i], 4, 8);
			}

			PieceList queens = board.queens[friendlyColourIndex];
			for (int i = 0; i < queens.Count; i++) {
				GenerateSlidingPieceMoves (queens[i], 0, 8);
			}

		}

		void GenerateSlidingPieceMoves (int startSquare, int startDirIndex, int endDirIndex) {
			bool isPinned = IsPinned (startSquare);

			// If this piece is pinned, and the king is in check, this piece cannot move 当前我方王被check并且这个棋子就是别针棋，则这个棋子不能移动
			if (inCheck && isPinned) {
				return;
			}

			for (int directionIndex = startDirIndex; directionIndex < endDirIndex; directionIndex++) {
				int currentDirOffset = directionOffsets[directionIndex]; // 遍历的方向

				// If pinned, this piece can only move along the ray towards/away from the friendly king, so skip other directions
                // 如果是别针棋子，那么这个棋子只能沿着和国王同一射线的方向移动，其他方向的格子直接跳过（因为还必须处于别针的状态用来block check）
				if (isPinned && !IsMovingAlongRay (currentDirOffset, friendlyKingSquare, startSquare)) {
					continue;
				}

				for (int n = 0; n < numSquaresToEdge[startSquare][directionIndex]; n++) {
					int targetSquare = startSquare + currentDirOffset * (n + 1);
					int targetSquarePiece = board.Square[targetSquare];

					// Blocked by friendly piece, so stop looking in this direction 向外遍历时，如果在该射线方向有我方棋子阻挡了去路，则跳出循环遍历
					if (Piece.IsColour (targetSquarePiece, friendlyColour)) {
						break;
					}
					bool isCapture = targetSquarePiece != Piece.None; // 剩下的则是对方的棋子，可以吃

					bool movePreventsCheck = SquareIsInCheckRay (targetSquare); // 判断格子区域是否是可check的区域
					if (movePreventsCheck || !inCheck) {
						if (genQuiets || isCapture) {
							moves.Add (new Move (startSquare, targetSquare));
						}
					}
					// If square not empty, can't move any further in this direction
					// Also, if this move blocked a check, further moves won't block the check
					if (isCapture || movePreventsCheck) {
						break;
					}
				}
			}
		}

        // 生成马的移动
		void GenerateKnightMoves () {
			PieceList myKnights = board.knights[friendlyColourIndex];

			for (int i = 0; i < myKnights.Count; i++) {
				int startSquare = myKnights[i];

				// Knight cannot move if it is pinned
                // 如果马作为别针棋子，则不能移动
				if (IsPinned (startSquare)) {
					continue;
				}

				for (int knightMoveIndex = 0; knightMoveIndex < knightMoves[startSquare].Length; knightMoveIndex++) {
					int targetSquare = knightMoves[startSquare][knightMoveIndex];
					int targetSquarePiece = board.Square[targetSquare];
					bool isCapture = Piece.IsColour (targetSquarePiece, opponentColour);
					if (genQuiets || isCapture) {
						// Skip if square contains friendly piece, or if in check and knight is not interposing/capturing checking piece
						// 可移动的目标格子有我方棋子或者正在被将军并且目标格子不是将军的位置，则跳过
						if (Piece.IsColour (targetSquarePiece, friendlyColour) || (inCheck && !SquareIsInCheckRay (targetSquare))) {
							continue;
						}
						moves.Add (new Move (startSquare, targetSquare));
					}
				}
			}
		}

		void GeneratePawnMoves () {
			PieceList myPawns = board.pawns[friendlyColourIndex];
			int pawnOffset = (friendlyColour == Piece.White) ? 8 : -8;
			int startRank = (board.WhiteToMove) ? 1 : 6;
			int finalRankBeforePromotion = (board.WhiteToMove) ? 6 : 1;

			int enPassantFile = ((int) (board.currentGameState >> 4) & 15) - 1;
			int enPassantSquare = -1;
			if (enPassantFile != -1) {
				enPassantSquare = 8 * ((board.WhiteToMove) ? 5 : 2) + enPassantFile;
			}

			for (int i = 0; i < myPawns.Count; i++) {
				int startSquare = myPawns[i];
				int rank = RankIndex (startSquare);
				bool oneStepFromPromotion = rank == finalRankBeforePromotion;

				if (genQuiets) {

					int squareOneForward = startSquare + pawnOffset; // 兵向前走一步

					// Square ahead of pawn is empty: forward moves  兵走棋
					if (board.Square[squareOneForward] == Piece.None) {
						// Pawn not pinned, or is moving along line of pin
						if (!IsPinned (startSquare) || IsMovingAlongRay (pawnOffset, startSquare, friendlyKingSquare)) {
							// Not in check, or pawn is interposing checking piece 不处于将军状态或者目标格子就是将军格子时
							if (!inCheck || SquareIsInCheckRay (squareOneForward)) {
								if (oneStepFromPromotion) {
                                    // 兵的升变
									MakePromotionMoves (startSquare, squareOneForward);
								} else {
                                    // 正常往前走
									moves.Add (new Move (startSquare, squareOneForward));
								}
							}

							// Is on starting square (so can move two forward if not blocked) 兵的第一次移子可以走两步
							if (rank == startRank) {
								int squareTwoForward = squareOneForward + pawnOffset;
								if (board.Square[squareTwoForward] == Piece.None) {
									// Not in check, or pawn is interposing checking piece
									if (!inCheck || SquareIsInCheckRay (squareTwoForward)) {
										moves.Add (new Move (startSquare, squareTwoForward, Move.Flag.PawnTwoForward));
									}
								}
							}
						}
					}
				}

				// Pawn captures. 兵吃子
				for (int j = 0; j < 2; j++) {
					// Check if square exists diagonal to pawn 兵只能吃对角线的子
					if (numSquaresToEdge[startSquare][pawnAttackDirections[friendlyColourIndex][j]] > 0) {
						// move in direction friendly pawns attack to get square from which enemy pawn would attack
						int pawnCaptureDir = directionOffsets[pawnAttackDirections[friendlyColourIndex][j]];
						int targetSquare = startSquare + pawnCaptureDir;
						int targetPiece = board.Square[targetSquare];

						// If piece is pinned, and the square it wants to move to is not on same line as the pin, then skip this direction
                        // 如果该格子已经作为别针，而且需要移动的位点不在和王一条射线上，则跳过 （因为此时王已经收到威胁了，别针的棋不能移走）
						if (IsPinned (startSquare) && !IsMovingAlongRay (pawnCaptureDir, friendlyKingSquare, startSquare)) {
							continue;
						}

						// Regular capture 普通吃子
						if (Piece.IsColour (targetPiece, opponentColour)) {
							// If in check, and piece is not capturing/interposing the checking piece, then skip to next square 处于将军状态并且当前目标格子不是将军格子
							if (inCheck && !SquareIsInCheckRay (targetSquare)) {
								continue;
							}
							if (oneStepFromPromotion) {
                                // 兵的升变
								MakePromotionMoves (startSquare, targetSquare);
							} else {
								moves.Add (new Move (startSquare, targetSquare));
							}
						}

						// Capture en-passant 吃过路兵
						if (targetSquare == enPassantSquare) {
							int epCapturedPawnSquare = targetSquare + ((board.WhiteToMove) ? -8 : 8);
							if (!InCheckAfterEnPassant (startSquare, targetSquare, epCapturedPawnSquare)) {
								moves.Add (new Move (startSquare, targetSquare, Move.Flag.EnPassantCapture));
							}
						}
					}
				}
			}
		}

        // 兵的升变
		void MakePromotionMoves (int fromSquare, int toSquare) {
			moves.Add (new Move (fromSquare, toSquare, Move.Flag.PromoteToQueen));
			if (promotionsToGenerate == PromotionMode.All) {
				moves.Add (new Move (fromSquare, toSquare, Move.Flag.PromoteToKnight));
				moves.Add (new Move (fromSquare, toSquare, Move.Flag.PromoteToRook));
				moves.Add (new Move (fromSquare, toSquare, Move.Flag.PromoteToBishop));
			} else if (promotionsToGenerate == PromotionMode.QueenAndKnight) {
				moves.Add (new Move (fromSquare, toSquare, Move.Flag.PromoteToKnight));
			}

		}

        // todo 判断startSquare和targetSquare连成的方向和rayDir是同一个方向
		bool IsMovingAlongRay (int rayDir, int startSquare, int targetSquare) {
			int moveDir = directionLookup[targetSquare - startSquare + 63];
			return (rayDir == moveDir || -rayDir == moveDir);
		}

		//bool IsMovingAlongRay (int directionOffset, int absRayOffset) {
		//return !((directionOffset == 1 || directionOffset == -1) && absRayOffset >= 7) && absRayOffset % directionOffset == 0;
		//}

        // 该格子是否处于别针棋状态
		bool IsPinned (int square) {
			return pinsExistInPosition && ((pinRayBitmask >> square) & 1) != 0;
		}

		bool SquareIsInCheckRay (int square) {
			return inCheck && ((checkRayBitmask >> square) & 1) != 0;
		}

		bool HasKingsideCastleRight {
			get {
				int mask = (board.WhiteToMove) ? 1 : 4;
				return (board.currentGameState & mask) != 0;
			}
		}

		bool HasQueensideCastleRight {
			get {
				int mask = (board.WhiteToMove) ? 2 : 8;
				return (board.currentGameState & mask) != 0;
			}
		}

        // 计算对方车，皇后和相在各自吃对方第一个棋子之前，搜寻走到的格子位点，记录在opponentSlidingAttackMap中
		void GenSlidingAttackMap () {
			opponentSlidingAttackMap = 0;

			PieceList enemyRooks = board.rooks[opponentColourIndex]; // 获取对手的棋子列表数据
			for (int i = 0; i < enemyRooks.Count; i++) {
				UpdateSlidingAttackPiece (enemyRooks[i], 0, 4); // 车走横纵向
			}

			PieceList enemyQueens = board.queens[opponentColourIndex];
			for (int i = 0; i < enemyQueens.Count; i++) {
				UpdateSlidingAttackPiece (enemyQueens[i], 0, 8); // 皇后走任意方向
			}

			PieceList enemyBishops = board.bishops[opponentColourIndex];
			for (int i = 0; i < enemyBishops.Count; i++) {
				UpdateSlidingAttackPiece (enemyBishops[i], 4, 8); // 相走斜向
			}
		}

        /// <summary>
        /// 标记startSquare处的棋子可以走到的位置，记到opponentSlidingAttackMap中，如果在遍历过程中遇到敌方的棋子，则终止遍历
        /// </summary>
		void UpdateSlidingAttackPiece (int startSquare, int startDirIndex, int endDirIndex) {

			for (int directionIndex = startDirIndex; directionIndex < endDirIndex; directionIndex++) {
				int currentDirOffset = directionOffsets[directionIndex];
				for (int n = 0; n < numSquaresToEdge[startSquare][directionIndex]; n++) {
					int targetSquare = startSquare + currentDirOffset * (n + 1);
					int targetSquarePiece = board.Square[targetSquare];
					opponentSlidingAttackMap |= 1ul << targetSquare;
					if (targetSquare != friendlyKingSquare) {
						if (targetSquarePiece != Piece.None) {
							break; // 当在棋盘上搜索到地方的棋子时，此遍历结束
						}
					}
				}
			}
		}

        /// <summary>
        /// 计算对方棋子可以攻击到的所有位点数据，能check到我方王的数据放在checkRayBitmask
        /// </summary>
		void CalculateAttackData () {
			GenSlidingAttackMap (); // 生成可滑行的棋子在攻击到敌方第一个棋子时所遍历的所有格子位点数据
			// Search squares in all directions around friendly king for checks/pins by enemy sliding pieces (queen, rook, bishop)
			int startDirIndex = 0;
			int endDirIndex = 8;

            // 根据对手是否还存在皇后 车 和相的情况，来确定搜索方向。如果存在皇后，则搜索0~8；否则check 若存在车和相，则0~8，只存在车则0~4，只存在相，则4~8，否则两者都不存在，不需要check远程攻击者
			if (board.queens[opponentColourIndex].Count == 0) {
				startDirIndex = (board.rooks[opponentColourIndex].Count > 0) ? 0 : 4;
				endDirIndex = (board.bishops[opponentColourIndex].Count > 0) ? 8 : 4;
			}

            // 遍历获取我方王受到攻击威胁的数据信息
			for (int dir = startDirIndex; dir < endDirIndex; dir++) {
				bool isDiagonal = dir > 3; // 4~7是对角线

				int n = numSquaresToEdge[friendlyKingSquare][dir]; // 获取我方王在各个方向的格子距离棋盘边缘的距离
				int directionOffset = directionOffsets[dir]; // 棋子的各个方向Index
				bool isFriendlyPieceAlongRay = false;
				ulong rayMask = 0;

                // 我方王 在选取的dir方向 纵深遍历每个格子，注意是从国王所在位置向外扩散的方式遍历的
				for (int i = 0; i < n; i++) {
					int squareIndex = friendlyKingSquare + directionOffset * (i + 1); // 每遍历一步就是一层
					rayMask |= 1ul << squareIndex;
					int piece = board.Square[squareIndex];

					// This square contains a piece 格子上包含棋子
					// 向外发散遍历的，如果在射线上有我方棋子会先被检测到，并且后面还有对方的棋子作为别针棋子
					if (piece != Piece.None) {
						if (Piece.IsColour (piece, friendlyColour)) { // 格子上是我方棋子
							// First friendly piece we have come across in this direction, so it might be pinned   https://www.chess.com/terms/pin-chess  在该射线方向上有我方棋子，有可能会作为王的别针棋子
							if (!isFriendlyPieceAlongRay) {
								isFriendlyPieceAlongRay = true;
							}
							// This is the second friendly piece we've found in this direction, therefore pin is not possible
							else {
								break;
							}
						}
						// This square contains an enemy piece 对方棋子
						else {
							int pieceType = Piece.PieceType (piece);

							// Check if piece is in bitmask of pieces able to move in current direction 棋子在对角并且是相或者皇后，或 棋子不在对角并且是车或者皇后时，说明敌方造成威胁
							if (isDiagonal && Piece.IsBishopOrQueen (pieceType) || !isDiagonal && Piece.IsRookOrQueen (pieceType)) {
								// Friendly piece blocks the check, so this is a pin
								if (isFriendlyPieceAlongRay) {
									pinsExistInPosition = true;
									pinRayBitmask |= rayMask; // 记录别针棋子信息
								}
								// No friendly piece blocking the attack, so this is a check 在这条射线方向上，如果没有别针棋子作为阻碍，则被将军
								else {
									checkRayBitmask |= rayMask; // 对方棋子（相，车或皇后）能check到我方王的位点，存在checkRayBitmask中
									inDoubleCheck = inCheck; // if already in check, then this is double check 二次将军
									inCheck = true;
								}
								break;
							} else {
								// This enemy piece is not able to move in the current direction, and so is blocking any checks/pins
								break;
							}
						}
					}
				}
				// Stop searching for pins if in double check, as the king is the only piece able to move in that case anyway
                // 二次将军，停止搜索遍历，这种情况下只有移动王
				if (inDoubleCheck) {
					break;
				}

			}

			// Knight attacks 对方马能移动到的位点
            // 对方马能check到我方王的所有位点，存在 checkRayBitmask 中
			PieceList opponentKnights = board.knights[opponentColourIndex];
			opponentKnightAttacks = 0;
			bool isKnightCheck = false;

			for (int knightIndex = 0; knightIndex < opponentKnights.Count; knightIndex++) {
				int startSquare = opponentKnights[knightIndex];
				opponentKnightAttacks |= knightAttackBitboards[startSquare];

				if (!isKnightCheck && BitBoardUtility.ContainsSquare (opponentKnightAttacks, friendlyKingSquare)) {
					isKnightCheck = true;
					inDoubleCheck = inCheck; // if already in check, then this is double check
					inCheck = true;
					checkRayBitmask |= 1ul << startSquare;
				}
			}

			// Pawn attacks 对方兵能移动到的位点
            // 对方兵能check到我方王的所有位点，存在 checkRayBitmask
			PieceList opponentPawns = board.pawns[opponentColourIndex];
			opponentPawnAttackMap = 0;
			bool isPawnCheck = false;

			for (int pawnIndex = 0; pawnIndex < opponentPawns.Count; pawnIndex++) {
				int pawnSquare = opponentPawns[pawnIndex];
				ulong pawnAttacks = pawnAttackBitboards[pawnSquare][opponentColourIndex];
				opponentPawnAttackMap |= pawnAttacks; // 当前棋盘上对方兵能攻击到位点Map

                // 如果对方兵能check，则记录到checkRayBitmask中
				if (!isPawnCheck && BitBoardUtility.ContainsSquare (pawnAttacks, friendlyKingSquare)) {
					isPawnCheck = true;
					inDoubleCheck = inCheck; // if already in check, then this is double check
					inCheck = true;
					checkRayBitmask |= 1ul << pawnSquare;
				}
			}

			int enemyKingSquare = board.KingSquare[opponentColourIndex];

			opponentAttackMapNoPawns = opponentSlidingAttackMap | opponentKnightAttacks | kingAttackBitboards[enemyKingSquare];
			opponentAttackMap = opponentAttackMapNoPawns | opponentPawnAttackMap;
		}

        // 判断给定的格子是否处于被攻击
		bool SquareIsAttacked (int square) {
			return BitBoardUtility.ContainsSquare (opponentAttackMap, square);
		}

        // 吃过路兵之后 check是否会将军
		bool InCheckAfterEnPassant (int startSquare, int targetSquare, int epCapturedPawnSquare) {
			// Update board to reflect en-passant capture
			board.Square[targetSquare] = board.Square[startSquare];
			board.Square[startSquare] = Piece.None;
			board.Square[epCapturedPawnSquare] = Piece.None;

			bool inCheckAfterEpCapture = false;
			if (SquareAttackedAfterEPCapture (epCapturedPawnSquare, startSquare)) {
				inCheckAfterEpCapture = true;
			}

			// Undo change to board
			board.Square[targetSquare] = Piece.None;
			board.Square[startSquare] = Piece.Pawn | friendlyColour;
			board.Square[epCapturedPawnSquare] = Piece.Pawn | opponentColour;
			return inCheckAfterEpCapture;
		}

        // 判断在吃过路兵之后该格子是否会被攻击
		bool SquareAttackedAfterEPCapture (int epCaptureSquare, int capturingPawnStartSquare) {
			if (BitBoardUtility.ContainsSquare (opponentAttackMapNoPawns, friendlyKingSquare)) {
				return true;
			}

			// Loop through the horizontal direction towards ep capture to see if any enemy piece now attacks king
			int dirIndex = (epCaptureSquare < friendlyKingSquare) ? 2 : 3;
			for (int i = 0; i < numSquaresToEdge[friendlyKingSquare][dirIndex]; i++) {
				int squareIndex = friendlyKingSquare + directionOffsets[dirIndex] * (i + 1);
				int piece = board.Square[squareIndex];
				if (piece != Piece.None) {
					// Friendly piece is blocking view of this square from the enemy.
					if (Piece.IsColour (piece, friendlyColour)) {
						break;
					}
					// This square contains an enemy piece
					else {
						if (Piece.IsRookOrQueen (piece)) {
							return true;
						} else {
							// This piece is not able to move in the current direction, and is therefore blocking any checks along this line
							break;
						}
					}
				}
			}

			// check if enemy pawn is controlling this square (can't use pawn attack bitboard, because pawn has been captured)
			for (int i = 0; i < 2; i++) {
				// Check if square exists diagonal to friendly king from which enemy pawn could be attacking it
				if (numSquaresToEdge[friendlyKingSquare][pawnAttackDirections[friendlyColourIndex][i]] > 0) {
					// move in direction friendly pawns attack to get square from which enemy pawn would attack
					int piece = board.Square[friendlyKingSquare + directionOffsets[pawnAttackDirections[friendlyColourIndex][i]]];
					if (piece == (Piece.Pawn | opponentColour)) // is enemy pawn
					{
						return true;
					}
				}
			}

			return false;
		}
	}

}