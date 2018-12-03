using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class RGRenderStateBlock
    {
        private const int gradientStencilRef = 64; //Use for gradient mask for VR
        private const int velocityStencilRef = 128;//Use for velocity pass

        public static readonly RenderStateBlock GradientStencilStateBlock = new RenderStateBlock
        {
            mask = RenderStateMask.Stencil,
            stencilReference = gradientStencilRef,
            stencilState = new StencilState
            {
                readMask = gradientStencilRef,
                enabled = true,
                compareFunction = CompareFunction.Equal,
                passOperation = StencilOp.Keep
            }
        };

        public static readonly RenderStateBlock GradientVelocityStencilStateBlock = new RenderStateBlock
        {
            mask = RenderStateMask.Stencil,
            stencilReference = gradientStencilRef | velocityStencilRef,
            stencilState = new StencilState
            {
                readMask = gradientStencilRef,
                writeMask = velocityStencilRef,
                enabled = true,
                compareFunction = CompareFunction.Equal,
                passOperation = StencilOp.Replace
            }
        };

        public static readonly RenderStateBlock VelocityStencilStateBlock = new RenderStateBlock
        {
            mask = RenderStateMask.Stencil,
            stencilReference = velocityStencilRef,
            stencilState = new StencilState
            {
                writeMask = velocityStencilRef,
                enabled = true,
                compareFunction = CompareFunction.Always,
                passOperation = StencilOp.Replace
            }
        };

        public static readonly RenderStateBlock CameraVelocityStencilStateBlock = new RenderStateBlock
        {
            mask = RenderStateMask.Stencil,
            stencilReference = velocityStencilRef,
            stencilState = new StencilState
            {
                readMask = velocityStencilRef,
                enabled = true,
                compareFunction = CompareFunction.NotEqual,
                passOperation = StencilOp.Replace
            }
        };

        public static readonly RenderStateBlock NoStencilStateBlock = new RenderStateBlock
        {
            mask = RenderStateMask.Stencil,
            stencilState = new StencilState
            {
                compareFunction = CompareFunction.Always,
                enabled = true
            }
        };
    }
}
