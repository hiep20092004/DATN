using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public static class ListExtensions
{
    public static void Fill<T>(this T[] array, T value)
    {
        for (var i = 0; i < array.Length; i++) array[i] = value;
    }

    public static void Fill<T>(this T[,] array, T value)
    {
        for (var i = 0; i < array.GetLength(0); i++)
        for (var j = 0; j < array.GetLength(1); j++)
            array[i, j] = value;
    }

    public static void Shuffle<T>(this IList<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = Random.Range(0, n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static Dictionary<TKey, TValue> Shuffle<TKey, TValue>(this Dictionary<TKey, TValue> dict)
    {
        // Bước 1: Copy vào list
        List<KeyValuePair<TKey, TValue>> list = new List<KeyValuePair<TKey, TValue>>(dict);

        // Bước 2: Fisher-Yates shuffle
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }

        // Bước 3: Tạo lại dictionary từ list
        Dictionary<TKey, TValue> shuffled = new Dictionary<TKey, TValue>();
        foreach (var pair in list)
        {
            shuffled.Add(pair.Key, pair.Value);
        }

        return shuffled;
    }

    public static void MoveItem<T>(this List<T> list, T item, int newIndex)
    {
        if (newIndex < 0 || newIndex >= list.Count)
            throw new ArgumentOutOfRangeException();

        int oldIndex = list.IndexOf(item);
        list.RemoveAt(oldIndex);
        if (newIndex > oldIndex)
            newIndex--;

        list.Insert(newIndex, item);
    }
}