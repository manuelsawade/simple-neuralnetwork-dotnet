﻿using ScratchNN.NeuralNetwork.Activations;
using ScratchNN.NeuralNetwork.CostFunctions;
using ScratchNN.NeuralNetwork.Extensions;
using ScratchNN.NeuralNetwork.Initializers;
using System.Diagnostics;
using System.Numerics.Tensors;

namespace ScratchNN.NeuralNetwork.Implementations;

public class AcceleratedNeuralNetwork : NeuralNetworkBase
{
    private readonly Random _random = new();
    private readonly ICostFunction _cost;
    private readonly IActivationFunction _activation;

    public override int Seed { get; init; }
    public override int[] Layers { get; init; }
    public override float[][] Biases { get; init; }
    public override float[][][] Weights { get; init; }

    public AcceleratedNeuralNetwork(
        int[] layers,
        IBiasInitializer biasInitializer,
        IWeightInitializer weightInitializer,
        ICostFunction cost,
        IActivationFunction activation,
        int? seed = null)
    {
        (_cost, _activation) = (cost, activation);
        (_random, Seed) = InitRandom(seed);

        Layers = layers;
        Biases = InitBiases(layers, _random, biasInitializer);
        Weights = InitWeights(layers, _random, weightInitializer);
    }

    public AcceleratedNeuralNetwork(
        int[] layers,
        float[][] biases,
        float[][][] weights,
        ICostFunction cost,
        IActivationFunction activation,
        int? seed = null)
    {
        (_cost, _activation) = (cost, activation);
        (_random, Seed) = InitRandom(seed);

        Layers = layers;
        Biases = biases;
        Weights = weights;
    }

    public override float[] Predict(float[] inputData)
    {
        var output = FeedForward(inputData).Outputs[^1];

        if (_cost is CrossEntropyCost)
            TensorPrimitives.SoftMax(output, output);

        return output;
    }


    public (float[][] Outputs, float[][] WeightedSums) FeedForward(float[] inputData)
    {
        var outputs = Layers.New<float>();
        outputs[0] = inputData;

        var weightedSums = Layers.New<float>();

        for (var iLayer = 1; iLayer < Layers.Length; iLayer++)
        {
            for (var iNeuron = 0; iNeuron < Layers[iLayer]; iNeuron++)
            {
                var inputs = outputs[iLayer - 1];
                var weights = Weights[iLayer][iNeuron];
                var bias = Biases[iLayer][iNeuron];

                weightedSums[iLayer][iNeuron] = TensorPrimitives.Dot(inputs, weights) + bias;
            }

            outputs[iLayer] = _activation.Compute(weightedSums[iLayer]);
        }

        return (outputs, weightedSums);
    }

    public void Fit(
        LabeledData[] trainingData,
        int epochs,
        int batchSize,
        float learningRate,
        float regularization)
    {
        var validationSetLength = (int)(trainingData.Length * 0.1);
        var validationData = trainingData
                .Shuffle(_random)
                .Take(validationSetLength)
                .ToArray();

        trainingData = trainingData.Skip(validationSetLength).ToArray();

        foreach (var epoch in Enumerable.Range(0, epochs))
        {
            var miniBatches = trainingData
                .Shuffle(_random)
                .Chunk(batchSize)
                .ToArray();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var iBatch = 0; iBatch < miniBatches.Length; iBatch++)
            {
                Console.Write($"Epoch {epoch,2} | Fit Batches: {iBatch}/{miniBatches.Length}");
                UpdateParameters(miniBatches[iBatch], learningRate, regularization);

                ConsoleExtensions.ClearCurrentLine();
            }

            stopwatch.Stop();
            var (accuracy, cost) = Evaluate(_cost ,validationData, regularization);

            Console.WriteLine($"Accuracy: {accuracy,-4} | Cost: {cost,-6} | Elapsed: {stopwatch.Elapsed}");
        }
    }

    public void UpdateParameters(LabeledData[] trainingBatch, float learningRate, float regularization)
    {
        float[][][] costSumBias = Biases
            .Shape()
            .ExpandBy(trainingBatch.Length)
            .New<float>();

        float[][][][] costSumWeights = Weights
            .Shape()
            .ExpandBy(trainingBatch.Length)
            .New<float>();

        Parallel.For(
            0,
            trainingBatch.Length,
            new() { MaxDegreeOfParallelism = trainingBatch.Length },
            (iData) =>
            {
                costSumBias[iData] = Biases.Shape().New<float>();
                costSumWeights[iData] = Weights.Shape().New<float>();

                (costSumBias[iData], costSumWeights[iData]) = Backpropagation(
                    trainingBatch[iData].InputData,
                    trainingBatch[iData].ExpectedData);
            });

        IterateNetwork(
            biasAction: (iLayer, biases) =>
            {
                ReadOnlySpan<float> biasSpan = biases;
                ReadOnlySpan<float> costs = costSumBias
                    .Select(costSum => costSum[iLayer])
                    .ToArray()
                    .Transpose()
                    .Select(cost => cost.Sum())
                    .ToArray();

                Biases[iLayer] = costs
                    .Multiply(learningRate)
                    .Divide(trainingBatch.Length)
                    .Subtract(biasSpan);
            },
            weightAction: (iLayer, iNeuron, weights) =>
            {
                ReadOnlySpan<float> weightSpan = weights;
                ReadOnlySpan<float> costs = costSumWeights
                    .Select(costSum => costSum[iLayer][iNeuron])
                    .ToArray()
                    .Transpose()
                    .Select(cost => cost.Sum())
                    .ToArray();

                var regularizationTerm = 1 - learningRate * (regularization / trainingBatch.Length);

                var costTerm = costs
                    .Multiply(learningRate)
                    .Divide(trainingBatch.Length);

                Weights[iLayer][iNeuron] = weights.Multiply(regularizationTerm).Subtract(costTerm);
            });
    }

    public (float[][], float[][][]) Backpropagation(float[] inputData, float[] expected)
    {
        var costsBias = Biases.Shape().New<float>();
        var costsWeights = Weights.Shape().New<float>();

        var (outputs, weightedSum) = FeedForward(inputData);

        var costs = _cost.Gradient(outputs[^1], expected, _activation.Gradient(weightedSum[^1]));

        costsBias[^1] = costs;
        costsWeights[^1] = costs.Multiply(weightedSum[^2].Transpose());

        for (var iLayer = Layers.Length - 2; iLayer > 0; iLayer--)
        {
            costs = Weights[iLayer + 1]
                .Transpose()
                .Multiply(costs)
                .Multiply(_activation.Gradient(weightedSum[iLayer]));

            costsBias[iLayer] = costs;
            costsWeights[iLayer] = costs.Multiply(outputs[iLayer - 1].Transpose());
        };

        return (costsBias, costsWeights);
    }
}
