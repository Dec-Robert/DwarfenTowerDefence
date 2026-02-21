using System.Collections.Generic;

public class PriorityQueue<T>
{
    private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();

    public int Count => elements.Count;

    public void Enqueue(T item, int priority)
    {
        elements.Add(new KeyValuePair<T, int>(item, priority));
        // Sortujemy listę tak, aby element z najmniejszym priorytetem (kosztem) by� pierwszy.
        // To prosta implementacja dla cel�w edukacyjnych.
        elements.Sort((x, y) => x.Value.CompareTo(y.Value));
    }

    public T Dequeue()
    {
        var item = elements[0].Key;
        elements.RemoveAt(0);
        return item;
    }
}