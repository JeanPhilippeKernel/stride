// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Core.Serialization.Contents;
using Xenko.Rendering;

namespace Xenko.Physics
{
    [ContentSerializer(typeof(DataContentSerializer<ConvexHullColliderShapeDesc>))]
    [DataContract("ConvexHullColliderShapeDesc")]
    [Display(500, "Convex Hull")]
    public class ConvexHullColliderShapeDesc : IAssetColliderShapeDesc
    {
#if XENKO_PLATFORM_WINDOWS_DESKTOP

        [Display(Browsable = false)]
#endif
        [DataMember(10)]
        public List<List<List<Vector3>>> ConvexHulls; // Multiple meshes -> Multiple Hulls -> Hull points

#if XENKO_PLATFORM_WINDOWS_DESKTOP

        [Display(Browsable = false)]
#endif
        [DataMember(20)]
        public List<List<List<uint>>> ConvexHullsIndices; // Multiple meshes -> Multiple Hulls -> Hull tris

        /// <userdoc>
        /// Model asset from where the engine will derive the convex hull.
        /// </userdoc>
        [DataMember(30)]
        public Model Model;

        /// <userdoc>
        /// The offset with the real graphic mesh.
        /// </userdoc>
        [DataMember(31)]
        public Vector3 LocalOffset;

        /// <userdoc>
        /// The local rotation of the collider shape.
        /// </userdoc>
        [DataMember(32)]
        public Quaternion LocalRotation = Quaternion.Identity;

        /// <userdoc>
        /// If this is checked the following parameters are totally ignored, as only a simple convex hull of the whole model will be generated.
        /// </userdoc>
        [DataMember(40)]
        [Display(Browsable = false)]
        public bool SimpleWrap = true;

        /// <userdoc>
        /// The scaling of the generated convex hull.
        /// </userdoc>
        [DataMember(45)]
        public Vector3 Scaling = Vector3.One;

        /// <userdoc>
        /// Control how many sub convex hulls will be created, more depth will result in a more complex decomposition.
        /// </userdoc>
        [DataMember(50)]
        [Display(Browsable = false)]
        public int Depth = 10;

        /// <userdoc>
        /// How many position samples to internally compute clipping planes ( the higher the more complex ).
        /// </userdoc>
        [DataMember(60)]
        [Display(Browsable = false)]
        public int PosSampling = 10;

        /// <userdoc>
        /// How many angle samples to internally compute clipping planes ( the higher the more complex ), nested with position samples, for each position sample it will compute the amount defined here.
        /// </userdoc>
        [DataMember(70)]
        [Display(Browsable = false)]
        public int AngleSampling = 10;

        /// <userdoc>
        /// If higher then 0 the computation will try to further improve the shape position sampling (this will slow down the process).
        /// </userdoc>
        [DataMember(80)]
        [Display(Browsable = false)]
        public int PosRefine = 5;

        /// <userdoc>
        /// If higher then 0 the computation will try to further improve the shape angle sampling (this will slow down the process).
        /// </userdoc>
        [DataMember(90)]
        [Display(Browsable = false)]
        public int AngleRefine = 5;

        /// <userdoc>
        /// Applied to the concavity during crippling plane approximation.
        /// </userdoc>
        [DataMember(100)]
        [Display(Browsable = false)]
        public float Alpha = 0.01f;

        /// <userdoc>
        /// Threshold of concavity, rising this will make the shape simpler.
        /// </userdoc>
        [DataMember(110)]
        [Display(Browsable = false)]
        public float Threshold = 0.01f;

        public bool Match(object obj)
        {
            var other = obj as ConvexHullColliderShapeDesc;
            if (other == null)
                return false;

            if (other.LocalOffset != LocalOffset || other.LocalRotation != LocalRotation)
                return false;

            return other.Model == Model &&
                   other.SimpleWrap == SimpleWrap &&
                   other.Scaling == Scaling &&
                   other.Depth == Depth &&
                   other.PosSampling == PosSampling &&
                   other.AngleSampling == AngleSampling &&
                   other.PosRefine == PosRefine &&
                   other.AngleRefine == AngleRefine &&
                   Math.Abs(other.Alpha - Alpha) < float.Epsilon &&
                   Math.Abs(other.Threshold - Threshold) < float.Epsilon;
        }

        public ColliderShape NewShapeFromDesc()
        {
            if (ConvexHulls == null) return null;
            ColliderShape shape;

            //Optimize performance and focus on less shapes creation since this shape could be nested

            if (ConvexHulls.Count == 1)
            {
                if (ConvexHulls[0].Count == 1 && ConvexHullsIndices[0][0].Count > 0)
                {
                    shape = new ConvexHullColliderShape(ConvexHulls[0][0], ConvexHullsIndices[0][0], Scaling)
                    {
                        NeedsCustomCollisionCallback = true,
                    };

                    //shape.UpdateLocalTransformations();
                    shape.Description = this;

                    return shape;
                }

                if (ConvexHulls[0].Count <= 1) return null;

                var subCompound = new CompoundColliderShape
                {
                    NeedsCustomCollisionCallback = true,
                };

                for (var i = 0; i < ConvexHulls[0].Count; i++)
                {
                    var verts = ConvexHulls[0][i];
                    var indices = ConvexHullsIndices[0][i];

                    if (indices.Count == 0) continue;

                    var subHull = new ConvexHullColliderShape(verts, indices, Scaling);
                    //subHull.UpdateLocalTransformations();
                    subCompound.AddChildShape(subHull);
                }

                //subCompound.UpdateLocalTransformations();
                subCompound.Description = this;

                return subCompound;
            }

            if (ConvexHulls.Count <= 1) return null;

            var compound = new CompoundColliderShape
            {
                NeedsCustomCollisionCallback = true,
            };

            for (var i = 0; i < ConvexHulls.Count; i++)
            {
                var verts = ConvexHulls[i];
                var indices = ConvexHullsIndices[i];

                if (verts.Count == 1)
                {
                    if (indices[0].Count == 0) continue;

                    var subHull = new ConvexHullColliderShape(verts[0], indices[0], Scaling);
                    //subHull.UpdateLocalTransformations();
                    compound.AddChildShape(subHull);
                }
                else if (verts.Count > 1)
                {
                    var subCompound = new CompoundColliderShape();

                    for (var b = 0; b < verts.Count; b++)
                    {
                        var subVerts = verts[b];
                        var subIndex = indices[b];

                        if (subIndex.Count == 0) continue;

                        var subHull = new ConvexHullColliderShape(subVerts, subIndex, Scaling);
                        //subHull.UpdateLocalTransformations();
                        subCompound.AddChildShape(subHull);
                    }

                    //subCompound.UpdateLocalTransformations();

                    compound.AddChildShape(subCompound);
                }
            }

            //compound.UpdateLocalTransformations();
            compound.Description = this;

            return compound;
        }
    }
}
