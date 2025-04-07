using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// This is a test comment

namespace OceanRendering
{
    [RequireComponent(typeof(MeshRenderer))]
    public class WaveGenerator : MonoBehaviour
    {
        [Header("Water Shader")]
        [SerializeField] private Shader _waterShader;

        [Header("Compute Shaders")]
        [SerializeField] private ComputeShader _butterflyTextureCompute;
        [SerializeField] private ComputeShader _fftCompute;
        [SerializeField] private ComputeShader _permuteAndScaleCompute;
        [SerializeField] private ComputeShader _initialSpectrumCompute;
        [SerializeField] private ComputeShader _movingSpectrumCompute;
        [SerializeField] private ComputeShader _slopeDebugCompute;

        [Header("Wave Properties")]
        [SerializeField] private TextureResolution _N;
        [SerializeField, Range(0, 50f)] private float _amp = 1.0f;
        [SerializeField, Range(0,3f)] private float _swell = 1.0f;
        [SerializeField, Range(0,2f)] private float _normalStrength = 1.0f;
        [SerializeField, Range(-1f,1f)] private float _foamCutoff = 0.0f;

        [Header("Rendering Settings")]
        [SerializeField] private Texture _envMap;

        [Header("SSS Settings")]
        [SerializeField, Range(-2f,2f)] private float _k1;
        [SerializeField, Range(-2,2)] private float _k2;
        [SerializeField, Range(-2,2)] private float _k3;
        [SerializeField, Range(-2,2)] private float _k4;
        [SerializeField, Range(-2,2)] private float _bubbleDensity;
        [SerializeField, Range(-2,2)] private float _roughness;
        [SerializeField] private Color _bubbleCol;
        [SerializeField] private Color _scatterCol;
        [SerializeField] private Color _sunCol;


        [Header("Other")]
        [SerializeField] private RawImage _rawImage;
        [SerializeField] private RawImage _rawImage2;
        [SerializeField] private RawImage _rawImage3;
        [SerializeField] private RawImage _rawImage4;
        [SerializeField] private RawImage _rawImage5;
        [SerializeField] private RawImage _rawImage6;

        private FastFourierTransformer _fourierTransformer;

        private Material _waterMaterial;
        private MeshRenderer _meshRenderer;

        // Random inital data
        private Vector2[] _uniformRandomArray;
        private ComputeBuffer _uniformRandomBuffer;

        // Spectrum parameters
        private PhillipsSpectrumParams _phillipsParams = new PhillipsSpectrumParams(
            100f, 38, new Vector2(1f,0.7f), 500, 500, 4, 0.0f);
        private PhillipsSpectrumParams[] _phillipsParamsArray = new PhillipsSpectrumParams[1];
        private ComputeBuffer _phillipsParamsBuffer;
        private List<ComputeBuffer> _computeBuffers = new List<ComputeBuffer>();

        // Kernel IDs
        private int _initialSpectrumKernelID;
        private int _initialConjugatedSpectrumKernelID;
        private int _movingSpectrumKernelID;
        private int _packNormalsKernelID;

        // Render Textures
        // height textures
        private RenderTexture _initialSpectrumRT;
        private RenderTexture _movingSpectrumRT;
        private RenderTexture _heightmapRT;

        // slope textures
        private RenderTexture _xSlopeSpectrumRT;
        private RenderTexture _zSlopeSpectrumRT;
        private RenderTexture _xSlopeRT;
        private RenderTexture _zSlopeRT;

        // displacement textures
        private RenderTexture _xDisplacementSpectrumRT;
        private RenderTexture _zDisplacementSpectrumRT;
        private RenderTexture _xDisplacementRT;
        private RenderTexture _zDisplacementRT;
        
        // displacement derivative textures
        private RenderTexture _xDxDisplacementSpectrumRT;
        private RenderTexture _zDzDisplacementSpectrumRT;
        private RenderTexture _xDzDisplacementSpectrumRT;
        private RenderTexture _xDxDisplacementRT;
        private RenderTexture _zDzDisplacementRT;
        private RenderTexture _xDzDisplacementRT;

        private List<RenderTexture> _renderTextures = new List<RenderTexture>();

        private int _totalPixels;
        private int _workGroupCount;

        // Message Functions
        private void Awake()
        {
            _totalPixels = _N.ToInt() * _N.ToInt();
            _workGroupCount = _N.ToInt() / MyConstants.THREADS;
            _uniformRandomArray = new Vector2[_totalPixels];
        }

        private void Start()
        {
            SetGlobalShaderConstants();

            _fourierTransformer = new FastFourierTransformer(_N.ToInt(), _butterflyTextureCompute, 
                _fftCompute, _permuteAndScaleCompute, _rawImage);

            GetKernelIDs();
            InitBuffers();
            InitRenderTextures();
            AssignShaderVariables();
            GenerateInitialSpectrum();
            InitWaterMaterial();

            // debug output images
            _rawImage2.texture = _initialSpectrumRT;
            _rawImage3.texture = _movingSpectrumRT;

        }

        private void Update()
        {
            SetDynamicMaterialProperties();

            GenerateMovingSpectrum();
            GenerateHeightmap();
            GenerateSlopeVector();
            GenerateDisplacementVector();
            GenerateDisplacementDerivatives();

            // debug output images
            _rawImage4.texture = _heightmapRT;
            _rawImage5.texture = _xSlopeRT;
            _rawImage6.texture = _zSlopeRT;
        }

        private void OnDisable()
        {
            _fourierTransformer.CleanupTextures();
            _fourierTransformer.CleanupBuffers();

            GameObject.Destroy(_waterMaterial);
            CleanupBuffers();
            CleanupTextures();
        }

        // Other methods
        private void InitWaterMaterial()
        {
            _waterMaterial = new Material(_waterShader);
            _waterMaterial.SetTexture("_heightmap", _heightmapRT);
            _waterMaterial.SetTexture("_xSlopeTexture", _xSlopeRT);
            _waterMaterial.SetTexture("_zSlopeTexture", _zSlopeRT);
            _waterMaterial.SetTexture("_xDisplacementTexture", _xDisplacementRT);
            _waterMaterial.SetTexture("_zDisplacementTexture", _zDisplacementRT);
            _waterMaterial.SetTexture("_xDxDisplacementTexture", _xDxDisplacementRT);
            _waterMaterial.SetTexture("_zDzDisplacementTexture", _zDzDisplacementRT);
            _waterMaterial.SetTexture("_xDzDisplacementtexture", _xDzDisplacementRT);
            _waterMaterial.SetTexture("_envMap", _envMap);

            _waterMaterial.SetFloat("_amp", _amp);
            _waterMaterial.SetFloat("_swell", _swell);
            _waterMaterial.SetFloat("_xLengthScale", _phillipsParams.Lx);
            _waterMaterial.SetFloat("_zLengthScale", _phillipsParams.Lz);

            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.material = _waterMaterial;
        }

        private void GetKernelIDs()
        {
            _initialSpectrumKernelID = _initialSpectrumCompute.FindKernel("GenerateInitialSpectrum");
            _initialConjugatedSpectrumKernelID  = _initialSpectrumCompute.FindKernel(
                "GenerateConjugatedInitialSpectrum");
            _movingSpectrumKernelID = _movingSpectrumCompute.FindKernel("GenerateMovingSpectrum");
        }

        private void InitBuffers()
        {
            // Uniform random data buffer
            _uniformRandomBuffer = new ComputeBuffer(_totalPixels, 8);
            _computeBuffers.Add(_uniformRandomBuffer);
            for (int i = 0; i < _totalPixels; i++)
            {
                float x = UnityEngine.Random.Range(0f, 1f);
                float y = UnityEngine.Random.Range(0f, 1f);
                _uniformRandomArray[i] = new Vector2(x,y);
            }
            _uniformRandomBuffer.SetData(_uniformRandomArray);

            // Spectrum params buffer
            _phillipsParamsBuffer = new ComputeBuffer(1,36);
            _computeBuffers.Add(_phillipsParamsBuffer);
            _phillipsParamsArray[0] = _phillipsParams;
            _phillipsParamsBuffer.SetData(_phillipsParamsArray);
        }

        private void SetGlobalShaderConstants()
        {
            Shader.SetGlobalFloat("_PI", MyConstants.PI);
            Shader.SetGlobalFloat("_GRAVITY", MyConstants.GRAVITY);
        }

        private void InitRenderTextures()
        {
            // height RTs
            _initialSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _movingSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _heightmapRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);

            // slopes RTs
            _xSlopeSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _zSlopeSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _xSlopeRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);
            _zSlopeRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);

            // displacement RTs
            _xDisplacementSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _zDisplacementSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _xDisplacementRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);
            _zDisplacementRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);

            // displacement derivs RTs
            _xDxDisplacementSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _zDzDisplacementSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _xDzDisplacementSpectrumRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Point, TextureWrapMode.Clamp);
            _xDxDisplacementRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);
            _zDzDisplacementRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);
            _xDzDisplacementRT = InitRT(_N.ToInt(), _N.ToInt(), FilterMode.Bilinear, TextureWrapMode.Repeat);
        }

        private RenderTexture InitRT(int width, int height, FilterMode filterMode, TextureWrapMode wrapMode)
        {
            RenderTexture rt = new RenderTexture(width, height,0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            rt.enableRandomWrite = true;
            rt.filterMode = filterMode;
            rt.wrapMode = wrapMode;
            rt.Create();

            _renderTextures.Add(rt);

            return rt;
        }

        private void AssignShaderVariables()
        {
            // Initial spectrum compute shader
            _initialSpectrumCompute.SetBuffer(_initialSpectrumKernelID, "_phillipsParams", 
                _phillipsParamsBuffer);
            _initialSpectrumCompute.SetBuffer(_initialSpectrumKernelID, "_uniformRandomData", 
                _uniformRandomBuffer);
            _initialSpectrumCompute.SetTexture(_initialSpectrumKernelID, "_initialSpectrumTexture",
                _initialSpectrumRT);
            _initialSpectrumCompute.SetTexture(_initialConjugatedSpectrumKernelID, "_initialSpectrumTexture",
                _initialSpectrumRT);
            _initialSpectrumCompute.SetInt("_N", _N.ToInt());

            // Moving spectrum compute shader
            _movingSpectrumCompute.SetBuffer(_movingSpectrumKernelID, "_phillipsParams", 
                _phillipsParamsBuffer);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_initialSpectrumTexture", 
                _initialSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_movingSpectrumTexture", 
                _movingSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_xSlopeSpectrumTexture",
                _xSlopeSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_zSlopeSpectrumTexture",
                _zSlopeSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_xDisplacementSpectrumTexture",
                _xDisplacementSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_zDisplacementSpectrumTexture",
                _zDisplacementSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_xDxDisplacementSpectrumTexture",
                _xDxDisplacementSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_zDzDisplacementSpectrumTexture",
                _zDzDisplacementSpectrumRT);
            _movingSpectrumCompute.SetTexture(_movingSpectrumKernelID, "_xDzDisplacementSpectrumTexture",
                _xDzDisplacementSpectrumRT);
            _movingSpectrumCompute.SetInt("_N", _N.ToInt());
        }

        private void GenerateInitialSpectrum()
        {
            _initialSpectrumCompute.Dispatch(_initialSpectrumKernelID, _workGroupCount, _workGroupCount, 1);
            _initialSpectrumCompute.Dispatch(_initialConjugatedSpectrumKernelID, _workGroupCount, _workGroupCount, 1);
        }

        private void GenerateMovingSpectrum()
        {
            _movingSpectrumCompute.SetFloat("_currentTime", Time.time);
            _movingSpectrumCompute.Dispatch(_movingSpectrumKernelID, _workGroupCount, _workGroupCount, 1);
        }

        private void GenerateHeightmap()
        {
            _fourierTransformer.IFFT2D(_movingSpectrumRT, _heightmapRT);
        }

        private void GenerateSlopeVector()
        {
            _fourierTransformer.IFFT2D(_xSlopeSpectrumRT, _xSlopeRT);
            _fourierTransformer.IFFT2D(_zSlopeSpectrumRT, _zSlopeRT);
        }

        private void GenerateDisplacementVector()
        {
            _fourierTransformer.IFFT2D(_xDisplacementSpectrumRT, _xDisplacementRT);
            _fourierTransformer.IFFT2D(_zDisplacementSpectrumRT, _zDisplacementRT);
        }

        private void GenerateDisplacementDerivatives()
        {
            _fourierTransformer.IFFT2D(_xDxDisplacementSpectrumRT, _xDxDisplacementRT);
            _fourierTransformer.IFFT2D(_zDzDisplacementSpectrumRT, _zDzDisplacementRT);
            _fourierTransformer.IFFT2D(_xDzDisplacementSpectrumRT, _xDzDisplacementRT);
        }

        private void SetDynamicMaterialProperties()
        {
            _waterMaterial.SetFloat("_swell", _swell);
            _waterMaterial.SetFloat("_amp", _amp);
            _waterMaterial.SetFloat("_normalStrength", _normalStrength);
            _waterMaterial.SetFloat("_foamCutoff", _foamCutoff);
        }

        private void CleanupBuffers()
        {
            foreach (ComputeBuffer cb in _computeBuffers)
            {
                if (cb != null)
                {
                    cb.Release();
                }
            }
        }

        private void CleanupTextures()
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
    }
}