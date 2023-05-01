using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LiquidSurface : MonoBehaviour {
        
    // It's important for unsafe operations to make sure data is set in the correct order
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Vertex {
        public float3 Position;
        public float3 Normal;
        public float2 Texcoord;
    }

    [Header("Springs")]
    public float SpringLength = 0.5f;
    public float SpringTension = 0.025f;
    public float SpringDampening = 0.025f;
    public float SpringSpread = 0.25f;  

    [Header("Surface")]
    [SerializeField] private float2 _surfaceBounds = new float2(10, 10);
    [SerializeField] private float _triSideLength = 0.5f;

    [Header("Texture")]
    [SerializeField] private int2 _resolution = new int2(128, 128);
        
    [Header("Testing")]
    [SerializeField] private float _strength = 0.05f;
    [SerializeField] private float _radius = 1.0f; 
 
    private Material _impactZone;
    private Material _liquidSpread;
    private Mesh _mesh;

    private RenderTexture _rt;
    private RenderTexture _spreadTexture;

    private MeshFilter _filter;

    public void Start() {
        // Prepare runtime resources
        CreateTextures();
        CreateMaterial();

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if(renderer != null) {
            Material matCopy = new Material(renderer.material);
            renderer.material = matCopy;
            matCopy.SetTexture("_LiquidSurface", _rt); // Set texture to our material. Read r value in shader to get the height
        }
    }

    public void Update() {
        // Testing / Method can be completly removed when triggering impacts over something else

        if(Input.GetMouseButton(0)) {
            Plane plane = new Plane(transform.up, transform.position);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            float hitDistance;
            if(plane.Raycast(ray, out hitDistance)) {
                Vector3 hitPoint = ray.GetPoint(hitDistance);

                Impact(hitPoint, _radius, _strength);
            }
        }
    }        
  
    public void OnValidate() {
        if(Application.isPlaying) return;

        // This will give an editor warning. Works anyways so who cares
        CreateMesh();
    }
       
    public void FixedUpdate() {
        // Using fixed update since update is a bit unreliable
        UpdateTexture();
    }

    //
    // Summary 
    //      Prepare the mesh of our surface. Texcoords will reach from 0 to 1
    public void CreateMesh() {
        if(_surfaceBounds.x <= 0 || _surfaceBounds.y <= 0 || _triSideLength == 0) return;

        // Create mesh if missing
        if(_mesh == null) {
            _mesh = new Mesh();
            _mesh.name = "Surface";
        }

        // Calculate secondary values
        float approxTriHeight = 0.86603f * _triSideLength;
        float halfSide = _triSideLength * 0.5f;

        // Calculate rows and columns
        int columns = (int) math.ceil(_surfaceBounds.x / _triSideLength) + 1;
        float xOffset = (((columns - 1) * _triSideLength + halfSide) % _surfaceBounds.x) * 0.5f;

        int rows = (int) math.ceil(_surfaceBounds.y / approxTriHeight) + 1;
        float yOffset = (((rows - 1) * approxTriHeight) % _surfaceBounds.y) * 0.5f;

        // Calculate geometry counts
        int vertexCount = columns * rows;
        int quadCount = (columns - 1) * (rows - 1);
        int triCount = quadCount * 2;
        int indexCount = triCount * 3;

        // Create temporary arrays to use with the Mesh API (need to dispose else you get a memory leak)
        NativeArray<Vertex> vertices = new NativeArray<Vertex>(vertexCount, Allocator.Temp);            
        NativeArray<short> indices = new NativeArray<short>(indexCount, Allocator.Temp);

        // Calculate positions of the vertices
        for(int i = 0; i < vertexCount; i++) {
            int column = i % columns;
            int row = i / columns;
                
            float x = column * _triSideLength + (halfSide * (row % 2)) - xOffset;
            float y = row * approxTriHeight - yOffset;

            if(column == 0) x = 0;
            else if(column == columns - 1) x = _surfaceBounds.x;

            if(row == 0) y = 0;
            else if(row == rows - 1) y = _surfaceBounds.y;

            float tx = x / _surfaceBounds.x;
            float ty = y / _surfaceBounds.y;

            vertices[i] = new Vertex {
                Position = new float3(x, 0, y),
                Normal = new float3(0, 1, 0),
                Texcoord = new float2(tx, ty)
            };
        }

        // Calculate indices
        int ind = 0;

        for(int i = 0; i < quadCount; i++) {
            int qc = i % (columns - 1);
            int qr = i / (columns - 1);

            short v0 = (short) (qr * columns + qc);
            short v1 = (short) (v0 + 1);
            short v2 = (short) (v0 + columns);
            short v3 = (short) (v2 + 1);

            bool evenRow = qr % 2 == 0;

            if(evenRow) {
                indices[ind++] = v0;
                indices[ind++] = v2;
                indices[ind++] = v1;
                indices[ind++] = v3;
                indices[ind++] = v1;
                indices[ind++] = v2;
            } else {
                indices[ind++] = v1;
                indices[ind++] = v0;
                indices[ind++] = v3;
                indices[ind++] = v2;
                indices[ind++] = v3;
                indices[ind++] = v0;
            }
        }

        // Attribute array to tell the API what to expect of our custom struct
        NativeArray<VertexAttributeDescriptor> attributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
        attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0);

        // Set buffers
        _mesh.SetVertexBufferParams(vertexCount, attributes);
        _mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
        _mesh.SetIndexBufferData(indices, 0, 0, indexCount);
                        
        // With the new API we need to set submeshes as well
        _mesh.subMeshCount = 1;
        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), MeshUpdateFlags.DontRecalculateBounds);
            
        // Use bounds from our settings; if it should stop rendering at low camera angles increase the y size
        _mesh.bounds = new Bounds(new float3(_surfaceBounds.x * 0.5f, 0.25f, _surfaceBounds.y * 0.5f), new float3(_surfaceBounds.x, 0.5f, _surfaceBounds.y));

        // Dispose temporary arrays to prevent memory leak; This will create no garbage
        attributes.Dispose();
        vertices.Dispose();
        indices.Dispose();

        // Set mesh to the filter
        if(_filter == null) _filter = GetComponent<MeshFilter>();
        _filter.sharedMesh = _mesh;
    }
        
    //
    // Summary 
    //      Prepare our two materials
    public void CreateMaterial() {
        //_material = new Material(_material);
        _impactZone = new Material(Shader.Find("Hidden/LiquidImpact"));
        _liquidSpread = new Material(Shader.Find("Hidden/LiquidSpread"));
        _liquidSpread.SetVector("_TexSize", new Vector4(_resolution.x, _resolution.y, 0, 0));
    }

    //
    // Summary 
    //      Prepare our two render textures
    private void CreateTextures() { 
        // We need this temporary texture to set the base values of the surface
        Texture2D baseValues = new Texture2D(1, 1);
        baseValues.SetPixel(0, 0, new Color(SpringLength, 0, 0, 0));
        baseValues.Apply();

        // We only need the R and G channel so we can use a higher precision for them
        _rt = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.RGHalf);
        _rt.filterMode = FilterMode.Bilinear;
        _rt.name = gameObject.name + "_Texture";
        Graphics.Blit(baseValues, _rt);

        _spreadTexture = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.RGHalf);
        _spreadTexture.filterMode = FilterMode.Bilinear;
        _spreadTexture.name = gameObject.name + "_LastFrame";
    }

    public void UpdateTexture() {
        _liquidSpread.SetVector("_Parameters", new Vector4(SpringLength, SpringTension, SpringDampening, SpringSpread));
        _liquidSpread.SetFloat("_DeltaTime", Time.deltaTime);

        // Cannot write to the same texture that is read so we need to swap (could also do a texture flip but relinking to shader doesn't feel cheaper...)
        Graphics.Blit(_rt, _spreadTexture, _liquidSpread);
        Graphics.Blit(_spreadTexture, _rt);
    }


    //
    // Summary 
    //      Add an impact to the surface
    public void Impact(Vector3 position, float radius, float strength) {
        float2 unitToTex = (1.0f / _surfaceBounds);

        // Get local coordinates from a world position
        Vector3 localPos = transform.InverseTransformPoint(position);
        float2 tCoord = new float2(localPos.x, localPos.z) / _surfaceBounds;
        float2 size = radius * unitToTex;

        // It's not possible to draw a material at a specific coordinate so we handle offset within the shader
        _impactZone.SetVector("_Parameters", new Vector4(tCoord.x, tCoord.y, size.x, size.y));
        _impactZone.SetFloat("_Strength", strength);                   

        // Draw the shape with it's parameters into the liquid texture (shader could be changed to any kind of shape)
        Graphics.Blit(null, _rt, _impactZone);
    }

    public void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(_surfaceBounds.x * 0.5f, 0.25f, _surfaceBounds.y * 0.5f), new Vector3(_surfaceBounds.x, 0.5f, _surfaceBounds.y));
    }

}