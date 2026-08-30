using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타르코프식 격자 보관함의 순수 로직. MonoBehaviour 가 아니라서 UI 없이도 테스트할 수 있다.
///
/// 좌표계: (0,0)이 좌상단, x는 오른쪽, y는 아래쪽으로 증가.
/// </summary>
[System.Serializable]
public class InventoryGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>칸마다 그 자리를 차지한 아이템 참조. 빈 칸은 null.</summary>
    private ItemInstance[,] cells;

    private readonly List<ItemInstance> items = new List<ItemInstance>();
    public IReadOnlyList<ItemInstance> Items => items;

    /// <summary>아이템이 들어오거나 나가거나 움직였을 때. UI가 이걸 듣고 다시 그린다.</summary>
    public event System.Action Changed;

    public InventoryGrid(int width, int height)
    {
        Resize(width, height);
    }

    public void Resize(int width, int height)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        cells = new ItemInstance[Width, Height];
        items.Clear();
        Changed?.Invoke();
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public ItemInstance GetAt(int x, int y) => InBounds(x, y) ? cells[x, y] : null;

    public bool Contains(ItemInstance item) => item != null && items.Contains(item);

    // ---------------- 배치 가능 검사 ----------------
    /// <summary>
    /// (x,y)를 좌상단으로 해서 item 을 놓을 수 있는지.
    /// </summary>
    /// <param name="ignore">이미 격자에 있는 아이템을 옮기는 중이라면 자기 자신은 빈 칸으로 친다.</param>
    public bool CanPlace(ItemInstance item, int x, int y, ItemInstance ignore = null)
    {
        if (item == null || item.data == null) return false;

        int w = item.Width, h = item.Height;
        if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                ItemInstance occupant = cells[x + dx, y + dy];
                if (occupant != null && occupant != ignore && occupant != item) return false;
            }
        }
        return true;
    }

    // ---------------- 배치 / 제거 ----------------
    public bool Place(ItemInstance item, int x, int y)
    {
        if (!CanPlace(item, x, y, item)) return false;

        // 이미 격자에 있던 아이템을 옮기는 경우라면 이전 자리를 먼저 비운다.
        // 이때 목록에는 그대로 남겨둬야 한다. 다시 Add 하면 같은 아이템이 목록에 두 번 들어가고,
        // UI 는 목록을 그대로 순회해서 그리므로 옮길 때마다 아이템이 하나씩 늘어난 것처럼 보인다.
        bool alreadyListed = items.Contains(item);
        if (alreadyListed) Clear(item);

        Fill(item, x, y);
        item.gridX = x;
        item.gridY = y;
        if (!alreadyListed) items.Add(item);

        Changed?.Invoke();
        return true;
    }

    public bool Remove(ItemInstance item)
    {
        if (item == null || !items.Contains(item)) return false;

        Clear(item);

        // 같은 아이템이 목록에 여러 번 들어가 있어도 남김없이 지운다.
        // (List.Remove 는 첫 번째 하나만 지우므로, 예전 저장 데이터에 중복이 있으면 유령이 남는다)
        items.RemoveAll(i => i == item);

        item.gridX = -1;
        item.gridY = -1;

        Changed?.Invoke();
        return true;
    }

    private void Fill(ItemInstance item, int x, int y)
    {
        for (int dx = 0; dx < item.Width; dx++)
            for (int dy = 0; dy < item.Height; dy++)
                cells[x + dx, y + dy] = item;
    }

    private void Clear(ItemInstance item)
    {
        if (item.gridX < 0 || item.gridY < 0) return;

        for (int dx = 0; dx < item.Width; dx++)
            for (int dy = 0; dy < item.Height; dy++)
            {
                int cx = item.gridX + dx, cy = item.gridY + dy;
                if (InBounds(cx, cy) && cells[cx, cy] == item) cells[cx, cy] = null;
            }
    }

    // ---------------- 자동 획득 ----------------
    /// <summary>
    /// 아이템을 주웠을 때 호출. 순서는
    ///  1) 같은 종류의 안 찬 더미에 부어넣기
    ///  2) 남은 게 있으면 빈 자리를 찾아 통째로 놓기
    ///  3) 회전을 허용하는 아이템이면 돌려서 한 번 더 시도
    /// </summary>
    /// <returns>전부 넣었으면 true. 자리가 모자라 일부/전부 못 넣었으면 false (item.count 에 남은 수량이 남는다).</returns>
    public bool TryAdd(ItemInstance item)
    {
        if (item == null || item.data == null) return false;

        // 1) 스택 합치기
        if (item.data.maxStack > 1)
        {
            for (int i = 0; i < items.Count && item.count > 0; i++)
                items[i].Absorb(item);

            if (item.count <= 0)
            {
                Changed?.Invoke();
                return true;
            }
        }

        // 2) 현재 방향으로 빈 자리 찾기
        if (TryPlaceAnywhere(item)) return true;

        // 3) 돌려서 재시도
        if (item.data.allowRotation)
        {
            item.rotated = !item.rotated;
            if (TryPlaceAnywhere(item)) return true;
            item.rotated = !item.rotated; // 실패했으면 원래대로
        }

        return false;
    }

    private bool TryPlaceAnywhere(ItemInstance item)
    {
        for (int y = 0; y <= Height - item.Height; y++)
            for (int x = 0; x <= Width - item.Width; x++)
                if (CanPlace(item, x, y, item))
                    return Place(item, x, y);

        return false;
    }

    /// <summary>비어 있는 칸 수. UI 하단에 "23/120" 같은 표시를 낼 때 쓴다.</summary>
    public int FreeCellCount()
    {
        int free = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (cells[x, y] == null) free++;
        return free;
    }

    public int TotalCellCount() => Width * Height;

    /// <summary>UI가 강제로 다시 그리게 할 때.</summary>
    public void RaiseChanged() => Changed?.Invoke();
}
