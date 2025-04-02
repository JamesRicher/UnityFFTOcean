using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using Unity.VisualScripting;

namespace OceanRendering
{
    public class FastFourierTransformer
    {
        private int _N; // texture resolution
        private int _totalStages;
        private int _workGroupCount;
        private ComputeShader _butterflyTextureCompute;
        private ComputeShader _fftCompute;
        private ComputeShader _permuteAndScaleCompute;

        private RenderTexture _butterflyRT;
        private RenderTexture _pingpong0RT; // Buffer textures for FFT computation
        private RenderTexture _pingpong1RT;
        private List<RenderTexture> _renderTextures = new List<RenderTexture>();
        
        private int[] _bitReversedIndices;
        private ComputeBuffer _bitReversedIndicesBuffer;

        // kernel IDs
        private int _butterflyTextureKernelID;
        private int _horButterflyKernelID;
        private int _verButterflyKernelID;
        private int _permuteAndScaleKernelID;

        private int _pingpong = 0;

        public FastFourierTransformer(int N, ComputeShader butterflyTextureCompute, 
            ComputeShader fftCompute, ComputeShader permuteAndScaleCompute, RawImage ri)
        {
            _N = N;
            _totalStages = (int)Mathf.Log(_N, 2);
            _workGroupCount = N / MyConstants.THREADS;

            _butterflyTextureCompute = butterflyTextureCompute;
            _fftCompute = fftCompute;
            _permuteAndScaleCompute = permuteAndScaleCompute;

            GetKernelIDs();
            InitRenderTextures();
            InitBuffers();
            AssignShaderVariables();
            GenerateButterflyTexture();

            ri.texture = _butterflyRT;
        }

        private void GetKernelIDs()
        {
            _butterflyTextureKernelID = _butterflyTextureCompute.FindKernel("GenerateButterflyTexture");
            _horButterflyKernelID = _fftCompute.FindKernel("HorizontalButterfly");
            _verButterflyKernelID = _fftCompute.FindKernel("VerticalButterfly");
            _permuteAndScaleKernelID = _permuteAndScaleCompute.FindKernel("PermuteAndScale");
        }

        private void InitRenderTextures()
        {
            _butterflyRT = InitRT(_totalStages, _N, FilterMode.Point);
            _renderTextures.Add(_butterflyRT);

            _pingpong0RT = InitRT(_N, _N, FilterMode.Point);
            _renderTextures.Add(_pingpong0RT);

            _pingpong1RT = InitRT(_N, _N, FilterMode.Point);
            _renderTextures.Add(_pingpong1RT);
        }

        private RenderTexture InitRT(int width, int height, FilterMode filterMode)
        {
            RenderTexture rt = new RenderTexture(width, height,0);
            rt.enableRandomWrite = true;
            rt.format = RenderTextureFormat.ARGBFloat;
            rt.filterMode = filterMode;
            rt.Create();

            return rt;
        }

        private void InitBuffers()
        {
            _bitReversedIndices = GenerateBitReversedArray(_N);
            _bitReversedIndicesBuffer = new ComputeBuffer(_N, 4);
            _bitReversedIndicesBuffer.SetData(_bitReversedIndices);
        }

        private void AssignShaderVariables()
        {
            // Butterfly texture compute
            _butterflyTextureCompute.SetInt("_N", _N);
            _butterflyTextureCompute.SetTexture(_butterflyTextureKernelID, "_butterflyTexture", _butterflyRT);
            _butterflyTextureCompute.SetBuffer(_butterflyTextureKernelID, "_bitReversedIndices", _bitReversedIndicesBuffer);

            // FFT compute
            _fftCompute.SetInt("_N", _N);
            _fftCompute.SetTexture(_horButterflyKernelID, "_butterflyTexture", _butterflyRT);
            _fftCompute.SetTexture(_horButterflyKernelID, "_pingpong0Texture", _pingpong0RT);
            _fftCompute.SetTexture(_horButterflyKernelID, "_pingpong1Texture", _pingpong1RT);
            _fftCompute.SetTexture(_verButterflyKernelID, "_butterflyTexture", _butterflyRT);
            _fftCompute.SetTexture(_verButterflyKernelID, "_pingpong0Texture", _pingpong0RT);
            _fftCompute.SetTexture(_verButterflyKernelID, "_pingpong1Texture", _pingpong1RT);

            // Permtue and scale compute
            _permuteAndScaleCompute.SetInt("_N", _N);
            _permuteAndScaleCompute.SetTexture(_permuteAndScaleKernelID, "_pingpong0Texture", _pingpong0RT);
            _permuteAndScaleCompute.SetTexture(_permuteAndScaleKernelID, "_pingpong1Texture", _pingpong1RT);
        }

        private void GenerateButterflyTexture()
        {
            _butterflyTextureCompute.Dispatch(_butterflyTextureKernelID, _totalStages, _workGroupCount, 1);
        }

        public void IFFT2D(RenderTexture input, RenderTexture output)
        {
            _pingpong = 0;
            Graphics.Blit(input, _pingpong0RT);

            for (int i = 0; i < _totalStages; i++)
            {
                _fftCompute.SetInt("_currentStage", i);
                _fftCompute.SetInt("_pingpong", _pingpong);
                _fftCompute.Dispatch(_horButterflyKernelID, _workGroupCount, _workGroupCount, 1);
                _pingpong = (_pingpong + 1) % 2;
            }

            for (int i = 0; i < _totalStages; i++)
            {
                _fftCompute.SetInt("_currentStage", i);
                _fftCompute.SetInt("_pingpong", _pingpong);
                _fftCompute.Dispatch(_verButterflyKernelID, _workGroupCount, _workGroupCount, 1);
                _pingpong = (_pingpong + 1) % 2;
            }

            _permuteAndScaleCompute.SetInt("_pingpong", _pingpong);
            _permuteAndScaleCompute.SetTexture(_permuteAndScaleKernelID, "_outputTexture", output);
            _permuteAndScaleCompute.Dispatch(_permuteAndScaleKernelID, _workGroupCount, _workGroupCount, 1);
        }

        // N should be a power of 2
        private int[] GenerateBitReversedArray(int N)
        {
            int[] output = new int[N];
            int totalBits = (int)Mathf.Log(N,2);

            for (int i = 0; i < N; i++)
            {
                output[i] = ReverseInteger(i, totalBits);
            }

            return output;
        }

        // bit-reverses an integer represented by totalBits bits
        private int ReverseInteger(int x, int totalBits)
        {
            string binaryX = Convert.ToString(x, 2).PadLeft(totalBits, '0');
            char[] binaryXArray = binaryX.ToCharArray();
            Array.Reverse(binaryXArray);
            string reversedBinaryX = new System.String(binaryXArray);

            int reversed = Convert.ToInt32(reversedBinaryX, 2);
            return reversed;
        }

        public void CleanupTextures()
        {
            foreach (RenderTexture rt in _renderTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    GameObject.Destroy(rt);
                }
            }
        }

        public void CleanupBuffers()
        {
            if (_bitReversedIndicesBuffer != null)
            {
                _bitReversedIndicesBuffer.Release();
            }
        }
    }
}
