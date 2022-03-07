using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chess.Game {
    // Player基类
	public abstract class Player {
		public event System.Action<Move> onMoveChosen;

        // 逻辑更新
		public abstract void Update ();

        // 被通知到可以移动棋子
		public abstract void NotifyTurnToMove ();

        // 主动移动棋子
		protected virtual void ChoseMove (Move move) {
			onMoveChosen?.Invoke (move);
		}
	}
}