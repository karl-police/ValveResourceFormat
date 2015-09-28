﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ValveResourceFormat.Blocks
{
    /// <summary>
    /// "RERL" block. ResourceExtRefList_t
    /// </summary>
    public class ResourceExtRefList : Block
    {
        public class ResourceReferenceInfo
        {
            /// <summary>
            /// Resource id.
            /// </summary>
            public ulong Id { get; set; }

            /// <summary>
            /// Resource name.
            /// </summary>
            public string Name { get; set; }

            public void WriteText(IndentedTextWriter writer)
            {
                writer.WriteLine("ResourceReferenceInfo_t");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("uint64 m_nId = 0x{0:X16}", Id);
                writer.WriteLine("CResourceString m_pResourceName = \"{0}\"", Name);
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        public readonly List<ResourceReferenceInfo> ResourceRefInfoList;

        public ResourceExtRefList()
        {
            ResourceRefInfoList = new List<ResourceReferenceInfo>();
        }

        public override BlockType GetChar()
        {
            return BlockType.RERL;
        }

        public override void Read(BinaryReader reader, Resource resource)
        {
            reader.BaseStream.Position = this.Offset;

            var offset = reader.ReadUInt32();
            var size = reader.ReadUInt32();

            reader.BaseStream.Position += offset - 8; // 8 is 2 uint32s we just read

            while (size-- > 0)
            {
                var resInfo = new ResourceReferenceInfo();
                resInfo.Id = reader.ReadUInt64();

                var previousPosition = reader.BaseStream.Position;

                // jump to string
                // offset is counted from current position,
                // so we will need to add 8 to position later
                reader.BaseStream.Position += reader.ReadInt64();

                resInfo.Name = reader.ReadNullTermString(Encoding.UTF8);

                ResourceRefInfoList.Add(resInfo);

                reader.BaseStream.Position = previousPosition + 8; // 8 is to account for string offset
            }
        }

        public override void WriteText(IndentedTextWriter writer)
        {
            writer.WriteLine("ResourceExtRefList_t");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Struct m_resourceRefInfoList[{0}] =", ResourceRefInfoList.Count);
            writer.WriteLine("[");
            writer.Indent++;

            foreach (var refInfo in ResourceRefInfoList)
            {
                refInfo.WriteText(writer);
            }

            writer.Indent--;
            writer.WriteLine("]");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
