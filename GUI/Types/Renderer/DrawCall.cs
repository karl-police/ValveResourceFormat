using OpenTK;
using OpenTK.Graphics.OpenGL;
using ValveResourceFormat.Blocks;

namespace GUI.Types.Renderer
{
    class DrawCall
    {
        public PrimitiveType PrimitiveType { get; set; }
        public uint BaseVertex { get; set; }
        //public uint VertexCount { get; set; }
        public uint StartIndex { get; set; }
        public int IndexCount { get; set; }
        //public float UvDensity { get; set; }     //TODO
        //public string Flags { get; set; }        //TODO
        public Vector3 TintColor { get; set; } = Vector3.One;

        public AABB? DrawBounds { get; set; }

        public int MeshId { get; set; }
        public int FirstMeshlet { get; set; }
        public int NumMeshlets { get; set; }
        public RenderMaterial OriginalMaterial { get; init; }
        public RenderMaterial Material { get; set; }
        public uint VertexArrayObject { get; set; }
        public VertexDrawBuffer VertexBuffer { get; set; }
        public DrawElementsType IndexType { get; set; }
        public IndexDrawBuffer IndexBuffer { get; set; }
    }

    internal struct IndexDrawBuffer
    {
        public uint Id;
        public uint Offset;
    }

    internal struct VertexDrawBuffer
    {
        public uint Id;
        public uint Offset;
        public uint ElementSizeInBytes;
        public VBIB.RenderInputLayoutField[] InputLayoutFields;
    }
}
