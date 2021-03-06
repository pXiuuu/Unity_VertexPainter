using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;


namespace VertexPainter
{
	/// <summary>
	/// MainGUI WindowClass
	/// </summary>
	public partial class VertexPainterWindow : EditorWindow
	{
		static Dictionary<string, bool> rolloutStates = new Dictionary<string, bool>();
		static GUIStyle rolloutStyle;


		public enum Tab
		{
			Paint = 0,
			Deform,
			Flow,
			Utility,
			Custom
		}

		private static string[] TAB_NAMES = {
			"PAINT",
			"DEFORM",
			"FLOW",
			"UTILITY",
            //"Custom"
        };

		static string sSwatchKey = "VertexPainter_Swatches";
		ColorSwatches swatches = default;

		private Tab tab = Tab.Paint;
		private bool hideMeshWireframe = false;
		private Vector2 scroll = default;

		private List<IVertexPainterUtility> utilities = new List<IVertexPainterUtility>();

		public bool DrawClearButton(string label)
		{
			if (GUILayout.Button(label, GUILayout.Width(60)))
			{
				return (EditorUtility.DisplayDialog("Confirm", "Clear " + label + " data?", "ok", "cancel"));
			}
			return false;
		}

		public static bool DrawRollup(string text, bool defaultState = true, bool inset = false)
		{
			if (rolloutStyle == null)
			{
				rolloutStyle = GUI.skin.box;
				rolloutStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
			}
			GUI.contentColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
			if (inset == true)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.GetControlRect(GUILayout.Width(40));
			}

			if (!rolloutStates.ContainsKey(text))
			{
				rolloutStates[text] = defaultState;
			}
			if (GUILayout.Button(text, rolloutStyle, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(20) }))
			{
				rolloutStates[text] = !rolloutStates[text];
			}
			if (inset == true)
			{
				EditorGUILayout.GetControlRect(GUILayout.Width(40));
				EditorGUILayout.EndHorizontal();
			}
			return rolloutStates[text];
		}

		private void DrawChannelGUI()
		{
			EditorGUILayout.Separator();
			GUI.skin.box.normal.textColor = Color.white;
			if (DrawRollup("Vertex Painter"))
			{
				bool oldEnabled = enabled;
				enabled = GUILayout.Toggle(enabled, "Active");
				if (enabled != oldEnabled)
				{
					InitMeshes();
					UpdateDisplayMode();
				}
				var oldShow = showVertexShader;
				EditorGUILayout.BeginHorizontal();
				showVertexShader = GUILayout.Toggle(showVertexShader, "Show Vertex Data");
				if (oldShow != showVertexShader)
				{
					UpdateDisplayMode();
				}
				bool emptyStreams = false;
				for (int i = 0; i < jobs.Length; ++i)
				{
					if (!jobs[i].HasStream())
					{
						emptyStreams = true;
					}
				}
				EditorGUILayout.EndHorizontal();
				if (emptyStreams)
				{
					if (GUILayout.Button("Add Streams"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							jobs[i].EnforceStream();
						}
						UpdateDisplayMode();
					}
				}


				{
					brushVisualization = (BrushVisualization)EditorGUILayout.EnumPopup("Brush Visualization", brushVisualization);
				}
				{
					EditorGUILayout.BeginHorizontal();
					showVertexPoints = GUILayout.Toggle(showVertexPoints, "Show Brush Influence");
					showVertexSize = EditorGUILayout.Slider(showVertexSize, 0.2f, 10);
					showVertexColor = EditorGUILayout.ColorField(showVertexColor, GUILayout.Width(40));
					EditorGUILayout.EndHorizontal();
				}
				{
					EditorGUILayout.BeginHorizontal();
					showNormals = GUILayout.Toggle(showNormals, "Show Normals");
					EditorGUILayout.EndHorizontal();
				}
				{
					EditorGUILayout.BeginHorizontal();
					showTangents = GUILayout.Toggle(showTangents, "Show Tangents");
					EditorGUILayout.EndHorizontal();
				}

				bool hasColors = false;
				bool hasUV0 = false;
				bool hasUV1 = false;
				bool hasUV2 = false;
				bool hasUV3 = false;
				bool hasPositions = false;
				bool hasNormals = false;
				bool hasStream = false;
				for (int i = 0; i < jobs.Length; ++i)
				{
					var stream = jobs[i]._stream;
					if (stream != null)
					{
						int vertexCount = jobs[i].verts.Length;
						hasStream = true;
						hasColors = (stream.colors != null && stream.colors.Length == vertexCount);
						hasUV0 = (stream.uv0 != null && stream.uv0.Count == vertexCount);
						hasUV1 = (stream.uv1 != null && stream.uv1.Count == vertexCount);
						hasUV2 = (stream.uv2 != null && stream.uv2.Count == vertexCount);
						hasUV3 = (stream.uv3 != null && stream.uv3.Count == vertexCount);
						hasPositions = (stream.positions != null && stream.positions.Length == vertexCount);
						hasNormals = (stream.normals != null && stream.normals.Length == vertexCount);
					}
				}

				if (hasStream && (hasColors || hasUV0 || hasUV1 || hasUV2 || hasUV3 || hasPositions || hasNormals))
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel("Clear Channel:");
					if (hasColors && DrawClearButton("Colors"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							stream.colors = null;
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasColors && DrawClearButton("RGB"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							Color[] src = jobs[i].meshFilter.sharedMesh.colors;
							int count = jobs[i].meshFilter.sharedMesh.colors.Length;
							for (int j = 0; j < count; ++j)
							{
								stream.colors[j].r = src[j].r;
								stream.colors[j].g = src[j].g;
								stream.colors[j].b = src[j].b;
							}
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasUV0 && DrawClearButton("UV0"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							stream.uv0 = null;
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasUV1 && DrawClearButton("UV1"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							stream.uv1 = null;
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasUV2 && DrawClearButton("UV2"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							stream.uv2 = null;
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasUV3 && DrawClearButton("UV3"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							var stream = jobs[i].stream;
							stream.uv3 = null;
							stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasPositions && DrawClearButton("Position"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							jobs[i].stream.positions = null;
							Mesh m = jobs[i].stream.GetModifierMesh;
							if (m != null)
								m.vertices = jobs[i].meshFilter.sharedMesh.vertices;
							jobs[i].stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}
					if (hasNormals && DrawClearButton("Normal"))
					{
						for (int i = 0; i < jobs.Length; ++i)
						{
							Undo.RecordObject(jobs[i].stream, "Vertex Painter Clear");
							jobs[i].stream.normals = null;
							jobs[i].stream.tangents = null;
							jobs[i].stream.Apply();
						}
						Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					}

					EditorGUILayout.EndHorizontal();
				}
				else if (hasStream)
				{
					if (GUILayout.Button("Remove Unused Stream Components"))
					{
						RevertMat();
						for (int i = 0; i < jobs.Length; ++i)
						{
							if (jobs[i].HasStream())
							{
								DestroyImmediate(jobs[i].stream);
							}
						}
						UpdateDisplayMode();
					}
				}

			}
			EditorGUILayout.Separator();
			GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
			EditorGUILayout.Separator();
		}

		private void DrawBrushSettingsGUI()
		{
			EditorGUILayout.Space();
			brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.01f, 30.0f);
			brushFlow = EditorGUILayout.Slider("Brush Flow", brushFlow, 0.1f, 128.0f);
			brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0.1f, 3.5f);

			if (tab == Tab.Paint && flowTarget != FlowTarget.ColorBA && flowTarget != FlowTarget.ColorRG)
			{
				flowRemap01 = EditorGUILayout.Toggle("use 0->1 mapping", flowRemap01);
			}
			EditorGUILayout.Separator();
			GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
			EditorGUILayout.Separator();
			EditorGUILayout.Space();
		}

		private void DrawCustomGUI()
		{
			scroll = EditorGUILayout.BeginScrollView(scroll);
		}

		private void DrawPaintGUI()
		{
			GUILayout.Box("Brush Settings", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(20) });
			var oldBM = brushMode;
			brushMode = (BrushTarget)EditorGUILayout.EnumPopup("Target Channel", brushMode);
			if (oldBM != brushMode)
			{
				UpdateDisplayMode();
			}

			if (brushMode == BrushTarget.Color || 
				brushMode == BrushTarget.UV0_AsColor || 
				brushMode == BrushTarget.UV1_AsColor || 
				brushMode == BrushTarget.UV2_AsColor || 
				brushMode == BrushTarget.UV3_AsColor)
			{
				brushColorMode = (BrushColorMode)EditorGUILayout.EnumPopup("Blend Mode", (System.Enum)brushColorMode);
				if (brushColorMode == BrushColorMode.Overlay || brushColorMode == BrushColorMode.Normal)
				{
					brushColor = EditorGUILayout.ColorField("Brush Color", brushColor);

					EditorGUILayout.Space();
					if (GUILayout.Button("Reset Palette", EditorStyles.miniButton, GUILayout.Width(120), GUILayout.Height(20)))
					{
						if (swatches != null)
						{
							DestroyImmediate(swatches);
						}
						swatches = ColorSwatches.CreateInstance<ColorSwatches>();
						EditorPrefs.SetString(sSwatchKey, JsonUtility.ToJson(swatches, false));
					}
					EditorGUILayout.Space();
					GUILayout.BeginHorizontal();
					for (int i = 0; i < swatches.colors.Length; ++i)
					{
						if (GUILayout.Button("", EditorStyles.textField, GUILayout.Width(16), GUILayout.Height(16)))
						{
							brushColor = swatches.colors[i];
						}
						EditorGUI.DrawRect(new Rect(GUILayoutUtility.GetLastRect().x + 1, GUILayoutUtility.GetLastRect().y + 1, 14, 14), swatches.colors[i]);
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal();
					for (int i = 0; i < swatches.colors.Length; i++)
					{
						if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(16), GUILayout.Height(12)))
						{
							swatches.colors[i] = brushColor;
							EditorPrefs.SetString(sSwatchKey, JsonUtility.ToJson(swatches, false));
						}
					}
					GUILayout.EndHorizontal();
				}
			}
			else if (brushMode == BrushTarget.ValueR || 
				brushMode == BrushTarget.ValueG || 
				brushMode == BrushTarget.ValueB || 
				brushMode == BrushTarget.ValueA)
			{
				brushValue = (int)EditorGUILayout.Slider("Brush Value", (float)brushValue, 0.0f, 256.0f);
			}
			else
			{
				floatBrushValue = EditorGUILayout.FloatField("Brush Value", floatBrushValue);
				var oldUVRange = uvVisualizationRange;
				uvVisualizationRange = EditorGUILayout.Vector2Field("Visualize Range", uvVisualizationRange);
				if (oldUVRange != uvVisualizationRange)
				{
					UpdateDisplayMode();
				}
			}
			//EditorGUILayout.Space();
			DrawBrushSettingsGUI();


			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Fill"))
			{
				if (OnBeginStroke != null)
				{
					OnBeginStroke(jobs);
				}
				for (int i = 0; i < jobs.Length; ++i)
				{
					Undo.RecordObject(jobs[i].stream, "Vertex Painter Fill");
					FillMesh(jobs[i]);
				}
				if (OnEndStroke != null)
				{
					OnEndStroke();
				}
				Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
			}
			if (GUILayout.Button("Random"))
			{
				for (int i = 0; i < jobs.Length; ++i)
				{
					Undo.RecordObject(jobs[i].stream, "Vertex Painter Fill");
					RandomMesh(jobs[i]);
				}
			}
			EditorGUILayout.EndHorizontal();

		}

		private void DrawDeformGUI()
		{
			GUILayout.Box("Brush Settings", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(20) });
			pull = (Event.current.shift);

			vertexMode = (VertexMode)EditorGUILayout.EnumPopup("Vertex Mode", vertexMode);
			vertexContraint = (VertexContraint)EditorGUILayout.EnumPopup("Vertex Constraint", vertexContraint);

			DrawBrushSettingsGUI();

			EditorGUILayout.LabelField(pull ? "Pull (shift)" : "Push (shift)");

		}

		private void DrawFlowGUI()
		{
			GUILayout.Box("Brush Settings", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(20) });
			var oldV = flowVisualization;
			flowVisualization = (FlowVisualization)EditorGUILayout.EnumPopup("Visualize", flowVisualization);
			if (flowVisualization != oldV)
			{
				UpdateDisplayMode();
			}
			var ft = flowTarget;
			flowTarget = (FlowTarget)EditorGUILayout.EnumPopup("Target", flowTarget);
			if (flowTarget != ft)
			{
				UpdateDisplayMode();
			}
			flowBrushType = (FlowBrushType)EditorGUILayout.EnumPopup("Mode", flowBrushType);

			DrawBrushSettingsGUI();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();



			if (GUILayout.Button("Reset"))
			{
				Vector2 norm = new Vector2(0.5f, 0.5f);

				foreach (PaintJob job in jobs)
				{
					PrepBrushMode(job);
					switch (flowTarget)
					{
						case FlowTarget.ColorRG:
							job.stream.SetColorRG(norm, job.verts.Length); break;
						case FlowTarget.ColorBA:
							job.stream.SetColorBA(norm, job.verts.Length); break;
						case FlowTarget.UV0_XY:
							job.stream.SetUV0_XY(norm, job.verts.Length); break;
						case FlowTarget.UV0_ZW:
							job.stream.SetUV0_ZW(norm, job.verts.Length); break;
						case FlowTarget.UV1_XY:
							job.stream.SetUV1_XY(norm, job.verts.Length); break;
						case FlowTarget.UV1_ZW:
							job.stream.SetUV1_ZW(norm, job.verts.Length); break;
						case FlowTarget.UV2_XY:
							job.stream.SetUV2_XY(norm, job.verts.Length); break;
						case FlowTarget.UV2_ZW:
							job.stream.SetUV2_ZW(norm, job.verts.Length); break;
						case FlowTarget.UV3_XY:
							job.stream.SetUV3_XY(norm, job.verts.Length); break;
						case FlowTarget.UV3_ZW:
							job.stream.SetUV3_ZW(norm, job.verts.Length); break;
					}
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

		}

		private void InitPluginUtilities()
		{
			if (utilities == null || utilities.Count == 0)
			{
				var interfaceType = typeof(IVertexPainterUtility);
				var all = System.AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(x => x.GetTypes())
				.Where(x => interfaceType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
				.Select(x => System.Activator.CreateInstance(x));

				foreach (var o in all)
				{
					IVertexPainterUtility u = o as IVertexPainterUtility;
					if (u != null && u.GetEnable())
					{
						utilities.Add(u);
					}
				}
				utilities = utilities.OrderBy(o => o.GetName()).ToList();
			}
		}

		private void DrawUtilityGUI()
		{
			InitPluginUtilities();
			for (int i = 0; i < utilities.Count; ++i)
			{
				var u = utilities[i];
				if (DrawRollup(u.GetName(), false))
				{
					u.OnGUI(jobs);
					EditorGUILayout.Space();
				}
			}
		}

		public void OnGUI()
		{

			if (Selection.activeGameObject == null)
			{
				EditorGUILayout.LabelField("No objects selected. Please select an object with a MeshFilter and Renderer");
				return;
			}

			if (swatches == null)
			{
				swatches = ColorSwatches.CreateInstance<ColorSwatches>();
				if (EditorPrefs.HasKey(sSwatchKey))
				{
					JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(sSwatchKey), swatches);
				}
				if (swatches == null)
				{
					swatches = ColorSwatches.CreateInstance<ColorSwatches>();
					EditorPrefs.SetString(sSwatchKey, JsonUtility.ToJson(swatches, false));
				}
			}

			DrawChannelGUI();

			Tab oldTab = tab;
			tab = (Tab)GUILayout.Toolbar((int)tab, TAB_NAMES);
			if (oldTab != tab)
			{
				UpdateDisplayMode();
			}

			if (tab == Tab.Paint)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				DrawPaintGUI();
			}
			else if (tab == Tab.Deform)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				DrawDeformGUI();
			}
			else if (tab == Tab.Flow)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				DrawFlowGUI();
			}
			else if (tab == Tab.Utility)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				DrawUtilityGUI();
			}
			EditorGUILayout.EndScrollView();
		}

		public void OnFocus()
		{
			if (painting)
			{
				EndStroke();
			}

			SceneView.duringSceneGui -= this.OnSceneGUI;
			SceneView.duringSceneGui += this.OnSceneGUI;

			Undo.undoRedoPerformed -= this.OnUndo;
			Undo.undoRedoPerformed += this.OnUndo;
			this.titleContent = new GUIContent("Vertex Paint");
			Repaint();
		}

		public void OnInspectorUpdate()
		{
			Repaint();
		}

		public void OnSelectionChange()
		{
			InitMeshes();
			this.Repaint();
		}

		public void OnDestroy()
		{
			bool show = showVertexShader;
			showVertexShader = false;
			UpdateDisplayMode();
			showVertexShader = show;
			DestroyImmediate(VertexInstanceStream.vertexShaderMaterial);
			SceneView.duringSceneGui -= this.OnSceneGUI;
		}
	}
}