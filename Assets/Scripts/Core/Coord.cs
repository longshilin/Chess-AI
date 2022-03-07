using System;
namespace Chess {
    // 坐标对象
    // 参考 Rank-File Mapping https://www.chessprogramming.org/Square_Mapping_Considerations
	public struct Coord : IComparable<Coord> {
		public readonly int fileIndex; // 横轴 字母序列 文件字母序列
		public readonly int rankIndex; // 纵轴 数字序列 排名数字序列

		public Coord (int fileIndex, int rankIndex) {
			this.fileIndex = fileIndex;
			this.rankIndex = rankIndex;
		}

        // 只有fileIndex和rankIndex相加和为奇数时才是白块区域
		public bool IsLightSquare () {
			return (fileIndex + rankIndex) % 2 != 0;
		}

        // 子块排序规则，主要用于判断两个坐标对象是否相等
		public int CompareTo (Coord other) {
			return (fileIndex == other.fileIndex && rankIndex == other.rankIndex) ? 0 : 1;
		}
	}
}