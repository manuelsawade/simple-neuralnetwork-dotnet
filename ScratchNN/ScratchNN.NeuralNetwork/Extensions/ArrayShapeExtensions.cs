﻿namespace ScratchNN.NeuralNetwork.Extensions;

internal static class ArrayShapeExtensions
{
    private static readonly Random rng = new();

    internal static T[] Shuffle<T>(this T[] source, Random? random = null)
    {
        random ??= rng;

        int n = source.Length;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (source[n], source[k]) = (source[k], source[n]);
        }

        return source;
    }

    public static int Shape<TType>(this TType[] source)
    {
        return source.Length;
    }

    public static int[] Shape<TType>(this TType[][] source)
    {
        return source.Select(Shape).ToArray();
    }

    public static int[][] Shape<TType>(this TType[][][] source)
    {
        return source.Select(Shape).ToArray();
    }

    public static int[][] ExpandBy(this int[] source, int size)
    {
        return Enumerable.Repeat(source, size).ToArray();
    }

    public static int[][][] ExpandBy(this int[][] source, int size)
    {
        return Enumerable.Repeat(source, size).ToArray();
    }

    public static TType[] New<TType>(this int source)
    {
        return new TType[source];
    }

    public static TType[][] New<TType>(this int[] source)
    {
        return source.Select(New<TType>).ToArray();
    }

    public static TType[][][] New<TType>(this int[][] source)
    {
        return source.Select(New<TType>).ToArray();
    }

    public static TType[][][][] New<TType>(this int[][][] source)
    {
        return source.Select(New<TType>).ToArray();
    }


    public static TType[][] Transpose<TType>(this TType[] source)
    {
        var result = new TType[source.Length][];

        for (var i = 0; i < source.Length; i++)
        {
            result[i] = [source[i]];
        }

        return result;
    }

    public static TType[][] Transpose<TType>(this TType[][] source)
    {
        var result = new TType[source[0].Length]
            .Select(r => new TType[source.Length])
            .ToArray();

        for (int i = 0; i < source.Length; i++)
        {
            for (int j = 0; j < source[i].Length; j++)
            {
                result[j][i] = source[i][j];
            }
        }

        return result;
    }
}
