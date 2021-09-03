﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TorchSharp.NN;
using TorchSharp.Tensor;
using Warp;
using Warp.NNModels;
using Warp.Tools;

namespace GANRecon
{
    class GANRecon
    {


        /*static TorchTensor relspaceProject(TorchTensor volume, TorchTensor angles, TorchTensor coordinates)
        {
            

        
        }*/

        static void Main(string[] args)
        {
            int boxLength = 64;
            int originalLength = -1;
            int2 boxsize = new(boxLength);
            int[] devices = { 1 };
            GPU.SetDevice(devices[0]);
            int batchSize = 16;
            int numEpochs = 1000;
            int discIters = 8;
            //var NoiseNet = new NoiseNet2DTorch(boxsize, devices, batchSize);

            //Read all particles and CTF information into memory
            {
                var directory = @"D:\GANRecon";
                var outdir = $@"{directory}\Debug_{boxLength}";

                if (!Directory.Exists(outdir))
                {
                    Directory.CreateDirectory(outdir);
                }

                var refVolume = Image.FromFile(@"D:\GANRecon\run_1k_unfil.mrc");
                originalLength = refVolume.Dims.X;
                var refMask = Image.FromFile(@"D:\GANRecon\mask_1k.mrc");

                refVolume.Multiply(refMask);
                refVolume = refVolume.AsScaled(new int3(boxLength));
                refMask = refMask.AsScaled(new int3(boxLength));
                float[] mask2 = Helper.ArrayOfFunction(i =>
                {
                    int x = i % boxLength;
                    int y = i / boxLength;

                    double cutoff = 45.0 / 180.0 * boxLength;
                    double sigma = 5.0;
                    double r = Math.Sqrt((float)(Math.Pow(x - (double)boxLength / 2.0, 2) + Math.Pow(y - (double)boxLength / 2.0, 2)));
                    if (r < cutoff)
                        return 1.0f;
                    else
                        return (float) Math.Max(0, (Math.Cos(Math.Min(1.0 , (r - cutoff) / sigma) * Math.PI) + 1.0) * 0.5);
                    /*if (r2 > Math.Pow(45.0 / 180.0*boxLength,2))
                        return 0.0f;
                    else
                        return 1.0f;*/
                }

                , boxLength * boxLength);
                
                TorchTensor tensorRefVolume = TensorExtensionMethods.ToTorchTensor(refVolume.GetHostContinuousCopy(), new long[] { 1, 1, boxLength, boxLength, boxLength }).ToDevice(TorchSharp.DeviceType.CUDA);

                TorchTensor tensorMaskSlice = TensorExtensionMethods.ToTorchTensor(mask2, new long[] { 1, 1, boxLength, boxLength }).ToDevice(TorchSharp.DeviceType.CUDA);
                Image imageMaskSlice = new Image(new int3(boxLength, boxLength, 1));
                GPU.CopyDeviceToDevice(tensorMaskSlice.DataPtr(), imageMaskSlice.GetDevice(Intent.Write), imageMaskSlice.ElementsReal);
                {


                    imageMaskSlice.WriteMRC($@"{outdir}\imageMaskSlice.mrc", true);

                }

                var model = new ReconstructionWGAN(new int2(boxLength), 10, devices, batchSize);
                model.getVolume();
                int startEpoch = 0;
                if (startEpoch > 0)
                {
                    model.Load($@"{outdir}\model_e{startEpoch}\model");
                }
                /*
                float3[] angles = Helper.ArrayOfFunction(i => new float3(0, (float)(i * 10.0 / 180.0 * Math.PI), 0), 10);
                float[] anglesFlat = Helper.ToInterleaved(angles);
                TorchTensor tensorAngles = TensorExtensionMethods.ToTorchTensor<float>(anglesFlat, new long[] { 10, 3 }).ToDevice(TorchSharp.DeviceType.CUDA);
                TorchTensor Projected = gen.ForwardParticle(Float32Tensor.Zeros(new long[] { 10 }, TorchSharp.DeviceType.CUDA), tensorAngles, true, 0.0d);

                Image imProjected = new Image(new int3(boxLength, boxLength, 10));
                GPU.CopyDeviceToDevice(Projected.DataPtr(), imProjected.GetDevice(Intent.Write), imProjected.ElementsReal);
                imProjected.WriteMRC($@"{directory}\imProjected.mrc", true);

                Projector proj = new(refVolume, 2);
                proj.ProjectToRealspace(new int2(boxLength), angles).WriteMRC($@"{directory}\imWarpProjected.mrc", true);
                */

                Star particles = new Star($@"{directory}\run_data.star");
                var randomSubset = particles.GetColumn("rlnRandomSubset").Select((s, i) => int.Parse(s)).ToArray();
                var angles = particles.GetRelionAngles();
                var offsets = particles.GetRelionOffsets();
                var Ctfs = particles.GetRelionCTF();

                var particlePaths = particles.GetRelionParticlePaths();

                var uniquePaths = Helper.ArrayOfFunction(i => particlePaths[i].Item1, particlePaths.Length).Distinct().ToArray();

                Dictionary<String, Image> stacksByPath = new();
                for (int i = 0; i < uniquePaths.Length; i++)
                {
                    //if(!stacksByPath.ContainsKey(particlePaths[i].Item1))
                    //{
                    Image im = Image.FromFile($@"{directory}\{uniquePaths[i]}");
                    var scaled = im.AsScaled(boxsize);
                    scaled.FreeDevice();
                    stacksByPath[uniquePaths[i]] = scaled;
                    im.Dispose();
                    //}
                }

                List<float> losses = new();
                {
                    int count = 0;
                    for (int i = 0; i < randomSubset.Length; i++)
                    {
                        if (true || randomSubset[i] == 1)
                        {
                            count++;
                        }
                    }

                    float3[] SubsetAngles = new float3[count];
                    CTF[] SubsetCTFs = new CTF[count];
                    float3[] SubsetOffsets = new float3[count];
                    Image[] SubsetParticles = new Image[count];

                    for (int i = 0, j = 0; i < randomSubset.Length; i++)
                    {
                        if (true || randomSubset[i] == 1)
                        {
                            SubsetAngles[j] = angles[i] * Helper.ToRad;
                            SubsetOffsets[j] = offsets[i];
                            SubsetCTFs[j] = Ctfs[i];
                            SubsetParticles[j] = stacksByPath[particlePaths[i].Item1].AsSliceXY(particlePaths[i].Item2);
                            SubsetParticles[j].MultiplySlices(imageMaskSlice);
                            SubsetParticles[j].ShiftSlices(new float3[] { offsets[i] * ((float)originalLength) / boxLength });
                            j++;
                        }
                    }

                    Projector proj = new Projector(refVolume, 2);
                    Image CTFCoords = CTF.GetCTFCoords(boxLength, originalLength);
                    Random rnd = new Random();
                    Image[] SubsetCleanParticles = Helper.ArrayOfFunction(i =>
                    {
                        Image im = proj.ProjectToRealspace(boxsize, new float3[] { SubsetAngles[i] });
                        im.Normalize();
                        im.FreeDevice();
                        return im;
                    }, count);
                    Image[] SubsetCtfs = Helper.ArrayOfFunction(i =>
                    {
                        Image im = new(new int3(boxLength, boxLength, 1), true);
                        GPU.CreateCTF(im.GetDevice(Intent.Write), CTFCoords.GetDevice(Intent.Read), IntPtr.Zero, (uint)CTFCoords.ElementsSliceComplex, new CTFStruct[] { SubsetCTFs[i].ToStruct() }, false, 1);
                        im.FreeDevice();
                        return im;
                    }, count);
                    TorchTensor randVolume = Float32Tensor.Random(tensorRefVolume.Shape, TorchSharp.DeviceType.CUDA);
                    //var gen = Modules.ReconstructionWGANGenerator(randVolume, boxLength, 10);
                    double learningRate = 1e-1;
                    //var optimizer = Optimizer.SGD(gen.GetParameters(), 1e-2, 0.0);
                    if(! Directory.Exists($@"{outdir}"))
                        Directory.CreateDirectory($@"{outdir}");
                    for (int epoch = startEpoch>0?startEpoch+1:0; epoch < numEpochs; epoch++)
                    {
                        float meanDiscLoss = 0.0f;
                        float meanRealLoss = 0.0f;
                        float meanFakeLoss = 0.0f;
                        float meanGenLoss = 0.0f;
                        int discSteps = 0;
                        int genSteps = 0;
                        /*if (epoch > 0 && epoch % 10 == 0)
                        {
                            learningRate = Math.Max(learningRate / 10, 1e-6);
                            optimizer.SetLearningRateSGD(learningRate);
                        }*/
                        for (int numBatch = 0; numBatch < count/batchSize; numBatch++)
                        {
                            //optimizer.ZeroGrad();
                            int[] thisBatch = Helper.ArrayOfFunction(i => rnd.Next(count), batchSize);

                            /*Image BatchParticles = Image.Stack(Helper.ArrayOfFunction(i=>SubsetParticles[thisBatch[i]],batchSize));
                            BatchParticles.ShiftSlices(Helper.ArrayOfFunction(i => SubsetOffsets[thisBatch[i]], batchSize));
                            BatchParticles.Normalize();
                            var BatchCTFStructs = Helper.ArrayOfFunction(i => SubsetCTFs[thisBatch[i]], batchSize);

                            Image BatchCTFs = new Image(new int3(boxLength * 2, boxLength * 2, batchSize), true);
                            GPU.CreateCTF(BatchCTFs.GetDevice(Intent.Write),
                                            CTFCoords.GetDevice(Intent.Read),
                                            IntPtr.Zero,
                                            (uint)CTFCoords.ElementsSliceComplex,
                                            BatchCTFStructs.Select(p => p.ToStruct()).ToArray(),
                                            false,
                                            (uint)BatchParticles.Dims.Z);
                            GPU.CheckGPUExceptions();*/
                            var BatchAngles = Helper.ArrayOfFunction(i => SubsetAngles[thisBatch[i]], batchSize);
                            //TorchTensor tensorAngles = TensorExtensionMethods.ToTorchTensor<float>(Helper.ToInterleaved(BatchAngles), new long[] { batchSize, 3 }).ToDevice(TorchSharp.DeviceType.CUDA);
                            //TorchTensor tensorRotMatrix = Modules.MatrixFromAngles(tensorAngles);
                            //TorchTensor projFake = gen.ForwardParticle(Float32Tensor.Empty(new long[] { 1 }), tensorAngles, true, 0);
                            Image source = Image.Stack(Helper.ArrayOfFunction(i => SubsetParticles[thisBatch[i]], batchSize));
                            Image sourceCTF = Image.Stack(Helper.ArrayOfFunction(i => SubsetCtfs[thisBatch[i]], batchSize));

                            //source.Normalize();
                            /*TorchTensor projReal = Float32Tensor.Empty(new long[] { batchSize, 1, boxLength, boxLength }, TorchSharp.DeviceType.CUDA);
                            GPU.CopyDeviceToDevice(source.GetDevice(Intent.Read), projReal.DataPtr(), source.ElementsReal);
                            projReal = projReal * tensorMaskSlice.Expand(new long[] { batchSize, -1, -1, -1 });
                            projFake = projFake * tensorMaskSlice.Expand(new long[] { batchSize, -1, -1, -1 });
                            TorchTensor diff = projReal - projFake;
                            TorchTensor diffSqrd = (diff).Pow(2);
                            TorchTensor loss = diffSqrd.Mean();
                            loss.Backward();*/
                            if (numBatch % discIters != 0) {
                                model.TrainDiscriminatorParticle(Helper.ToInterleaved(BatchAngles), source, sourceCTF, (float)0.0001, (float)2, out Image prediction, out float[] wLoss, out float[] rLoss, out float[] fLoss);
                                GPU.CheckGPUExceptions();
                                float discLoss = wLoss[0];
                                meanDiscLoss += (float)discLoss;
                                meanRealLoss += (float)rLoss[0];
                                meanFakeLoss += (float)fLoss[0];
                                discSteps++;
                                //prediction.WriteMRC($@"{outdir}\prediction_{epoch}_{numBatch}.mrc", true);
                                prediction.Dispose();
                            }
                            else
                            {
                                model.TrainGeneratorParticle(Helper.ToInterleaved(BatchAngles), sourceCTF, source, (float)0.0001, out Image prediction, out Image predictionNoisy, out float[] genLoss);
                                GPU.CheckGPUExceptions();
                                meanGenLoss += genLoss[0];
                                genSteps++;
                                prediction.Dispose();
                            }
                            //prediction.WriteMRC($@"{directory}\Optimization\prediction_{epoch}_{numBatch}.mrc", true);
                            //source.WriteMRC($@"{outdir}\source_{epoch}_{numBatch}.mrc", true);
                            //sourceCTF.WriteMRC($@"{outdir}\sourceCTF_{epoch}_{numBatch}.mrc", true);
                            //optimizer.Step();



                            /*if (numBatch == 0)
                            {
                                Image projectionsReal = new Image(new int3(boxLength, boxLength, batchSize));
                                Image projectionsFake = new Image(new int3(boxLength, boxLength, batchSize));

                                GPU.CopyDeviceToDevice(projReal.DataPtr(), projectionsReal.GetDevice(Intent.Write), projectionsReal.ElementsReal);
                                GPU.CopyDeviceToDevice(projFake.DataPtr(), projectionsFake.GetDevice(Intent.Write), projectionsFake.ElementsReal);

                                Image stacked = Image.Stack(new Image[] { projectionsReal, projectionsFake, source });

                                stacked.WriteMRC($@"{directory}\Optimization\Projections_{epoch}.mrc", true);

                                projectionsFake.Dispose();
                                projectionsReal.Dispose();
                                stacked.Dispose();

                                Image vol = new Image(new int3(boxLength));
                                GPU.CopyDeviceToDevice(zeroVolume.DataPtr(), vol.GetDevice(Intent.Write), vol.ElementsReal);
                                vol.WriteMRC($@"{directory}\Optimization\Volume_{epoch}.mrc", true);

                            }*/

                            //BatchParticles.Dispose();
                            //BatchCTFs.Dispose();
                            source.Dispose();

                            //tensorAngles.Dispose();
                            //tensorRotMatrix.Dispose();
                            //projFake.Dispose();
                            //projReal.Dispose();
                            //diff.Dispose();
                            //diffSqrd.Dispose();
                            //loss.Dispose();
                        }
                        losses.Append(meanDiscLoss / (count / batchSize));
                        GPU.DeviceSynchronize();
                        GPU.CheckGPUExceptions();
                        using (StreamWriter file = new($@"{ outdir }\log.txt", append: true))
                        {
                            file.WriteLineAsync($"Epoch {epoch}: Disc: { meanDiscLoss / discSteps } (r: {meanRealLoss / discSteps}, f: {meanFakeLoss / discSteps}) Gen: {meanGenLoss / genSteps}");
                        }
                        //Console.WriteLine($"Epoch {epoch}: Disc: { meanDiscLoss / discSteps } Gen: {meanGenLoss / genSteps}");
                        
                        if(epoch %50==0 || epoch == numEpochs-1)
                            model.Save($@"{outdir}\model_e{epoch}\model");
                    }



                }
              
            }
        }
    }
}
