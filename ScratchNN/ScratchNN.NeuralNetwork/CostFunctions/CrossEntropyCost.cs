﻿using ScratchNN.NeuralNetwork.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace ScratchNN.NeuralNetwork.CostFunctions;

public class CrossEntropyCost : ICostFunction
{
    public float Computation(float[] output, float[] expected)
    {
        ReadOnlySpan<float> expectedSpan = expected;
        ReadOnlySpan<float> outputSpan = output;

        var penaltyTermForOneLabel = expectedSpan.Negate().Multiply(outputSpan.Log());
        var penaltyTermForZeroLabel = expectedSpan.OneSubtract().Multiply(outputSpan.OneSubtract().Log());

        return penaltyTermForOneLabel.Subtract(penaltyTermForZeroLabel).Sum(NanToNum);
    }

    private static float NanToNum(float number) => number switch
    {
        float.NaN => 0.0f,
        float.PositiveInfinity => float.MaxValue,
        float.NegativeInfinity => float.MinValue,
        _ => number
    };


    public float[] Cost(float[] output, float[] expected, float[] weightedSum)
    {
        return output.Subtract(expected);
    }
}