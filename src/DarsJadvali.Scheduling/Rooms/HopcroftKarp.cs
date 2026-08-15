namespace DarsJadvali.Scheduling.Rooms;

/// <summary>
/// Hopcroft–Karp bipartite maximum matching, O(E*sqrt(V)).
/// Xona tayinlash fazasi uchun (02-asc-.., 4.5; aSc `#1855 Assigning classrooms`).
/// </summary>
public static class HopcroftKarp
{
    private const int Nil = -1;

    /// <summary>
    /// <paramref name="adjacency"/>[left] -> right tugunlar ro'yxati.
    /// Natija: <c>matchLeft[left]</c> = right yoki -1.
    /// </summary>
    public static int[] Match(int leftCount, int rightCount, IReadOnlyList<int>[] adjacency, out int matchedCount)
    {
        var matchLeft = new int[leftCount];
        var matchRight = new int[rightCount];
        Array.Fill(matchLeft, Nil);
        Array.Fill(matchRight, Nil);

        var dist = new int[leftCount];
        var queue = new Queue<int>();
        matchedCount = 0;

        while (Bfs(leftCount, adjacency, matchLeft, matchRight, dist, queue))
        {
            for (int u = 0; u < leftCount; u++)
                if (matchLeft[u] == Nil && Dfs(u, adjacency, matchLeft, matchRight, dist))
                    matchedCount++;
        }
        return matchLeft;
    }

    private static bool Bfs(int leftCount, IReadOnlyList<int>[] adjacency,
                            int[] matchLeft, int[] matchRight, int[] dist, Queue<int> queue)
    {
        queue.Clear();
        bool found = false;
        for (int u = 0; u < leftCount; u++)
        {
            if (matchLeft[u] == Nil) { dist[u] = 0; queue.Enqueue(u); }
            else dist[u] = int.MaxValue;
        }

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            var adj = adjacency[u];
            for (int i = 0; i < adj.Count; i++)
            {
                int v = adj[i];
                int w = matchRight[v];
                if (w == Nil) found = true;
                else if (dist[w] == int.MaxValue)
                {
                    dist[w] = dist[u] + 1;
                    queue.Enqueue(w);
                }
            }
        }
        return found;
    }

    private static bool Dfs(int u, IReadOnlyList<int>[] adjacency, int[] matchLeft, int[] matchRight, int[] dist)
    {
        var adj = adjacency[u];
        for (int i = 0; i < adj.Count; i++)
        {
            int v = adj[i];
            int w = matchRight[v];
            if (w == Nil || (dist[w] == dist[u] + 1 && Dfs(w, adjacency, matchLeft, matchRight, dist)))
            {
                matchLeft[u] = v;
                matchRight[v] = u;
                return true;
            }
        }
        dist[u] = int.MaxValue;
        return false;
    }
}
