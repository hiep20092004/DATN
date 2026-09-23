Shader "Shader Graphs/WaterGraphTube" {
	Properties {
		Vector1_ab5bbc78aace46cca47c976624c57654 ("Distortion", Float) = 0.01
		Vector1_f64f7fc51f514d1fa0c302cd1c54ace5 ("Smoothness", Float) = 0
		Vector1_7f26fb54a3f1446cae706364d1d4c87b ("DistortionVertex", Float) = 0.1
		[NoScaleOffset] [Normal] Texture2D_df814fb1718447f4b430f7939cffe52f ("NormalMap", 2D) = "bump" {}
		Vector1_ddb6293edce643b0ad001ef8e4bfe970 ("NormalStrength", Float) = 0
		Vector1_30c2129fc1b14cacae4c5402852fa917 ("SpecularFloat", Float) = 0
		Vector1_ca96e47f336c4f3e94f6e18dd5768814 ("NormalTiling", Float) = 0
		_Disolve ("Disolve", Range(0, 1)) = 0.1
		_ReverseDisolve ("ReverseDisolve", Range(0, 1)) = 0.9
		Vector1_a34d470e309a4e6196963d7f1b300204 ("CenterConnect", Float) = 0.08
		_BaseColor ("BaseColor", Vector) = (1,0,0,1)
		_Metalic ("Metalic", Float) = 0
		_AmbientOcclusion ("AmbientOcclusion", Float) = 0
		_WaterSpeed ("WaterSpeed", Vector) = (0,0.2,0,0)
		_NormalSpeed ("NormalSpeed", Vector) = (0,-0.2,0,0)
		_RandomDissolveNoiseScale ("RandomDissolveNoiseScale", Float) = 10
		_RandomDissolve ("RandomDissolve", Float) = 0.5
		_GradientNoiseScale ("GradientNoiseScale", Float) = 10
		[HideInInspector] _SrcBlend ("__src", Float) = 1
		[HideInInspector] _DstBlend ("__dst", Float) = 0
		[HideInInspector] _ZWrite ("__zw", Float) = 1
		[HideInInspector] _BUILTIN_QueueOffset ("Float", Float) = 0
		[HideInInspector] _BUILTIN_QueueControl ("Float", Float) = -1
	}
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			Cull Off
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _BaseColor;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _BaseColor;
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}
