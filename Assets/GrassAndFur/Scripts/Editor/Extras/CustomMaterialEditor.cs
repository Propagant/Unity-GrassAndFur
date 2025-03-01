using UnityEngine;
using UnityEditor;

namespace GrassAndFur.UEditor
{
    public sealed class CustomMaterialEditor : ShaderGUI
    {
        #region Custom Shader Features

        /// <summary>
        /// Shows tiling & offset fields below specific texture (it's hidden as default)
        /// </summary>
        private const string att_ShowScaleOffset = "SHOWSCALEOFFSET";

        /// <summary>
        /// Conditional attribute for shader properties.
        /// Eg: [SHOWIF_1_ShaderType].
        /// Formal Eg: [SHOWIF_VALUE,VALUE2_PROPERTYNAME].
        /// Negation Eg (use # before property name): [SHOWIF_VALUE,VALUE2,VALUE3_#PROPERTYNAME].
        /// 
        /// All the multiple conditional values use 'OR' operator.
        /// Supports Int, Float and Texture property types.
        /// </summary>
        private const string att_ShowIf = "SHOWIF_";

        /// <summary>
        /// Space attribute for making a space between properties
        /// </summary>
        private const string att_Space = "Space";

        #endregion

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material currentMaterial = (Material)materialEditor.target;
            
            DrawV();
            DrawL("Custom Shader Editor", LabelStyle.Bold);
            DrawL("Written by Matej Vanco 2022", LabelStyle.Italic);
            DrawS();

            if(properties.Length == 0)
            {
                DrawL("No properties...");
                DrawVE();
                return;
            }

            DrawV();

            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i].flags != MaterialProperty.PropFlags.HideInInspector)
                    ProcessCustomAttributes(properties[i], i, materialEditor, currentMaterial, properties);
            }

            DrawS();
            DrawVE();
            DrawVE();
        }

        private void ProcessCustomAttributes(in MaterialProperty currentProperty, in int index, in MaterialEditor matEditor, in Material matSender, in MaterialProperty[] matProps)
        {
            string propName = currentProperty.name;
            
            bool propIsTexture = currentProperty.type == MaterialProperty.PropType.Texture;
            string[] propAttributes = matSender.shader.GetPropertyAttributes(index);

            bool drawDefaultProperty = true;

            // Draw conditional properties first
            for (int i = 0; i < propAttributes.Length; i++)
            {
                string atName = propAttributes[i];
                if (!atName.StartsWith(att_ShowIf))
                    continue;
                atName = atName.Replace(att_ShowIf, "");
                string atValue = atName.Substring(0, atName.IndexOf('_'));

                string[] atValues = atValue.Split(',');
                string atProperty = atName.Substring(atValue.Length, atName.Length - atValue.Length);

                bool negation = atProperty.Contains("#");
                if (negation)
                    atProperty = atProperty.Replace("#","");

                if (!FindConditionalTargetProperty(matProps, atProperty, out MaterialProperty targetProperty))
                {
                    Debug.LogError($"Property '{propName}' cannot find its conditional target property '{atProperty}' in shader '{matSender.shader.name}'!");
                    continue;
                }

                drawDefaultProperty = false;

                bool result = GetConditionalPropertyResult(targetProperty, atValues);
                if (!((result && !negation) || (!result && negation)))
                    continue;

                DrawIdentPlus();
                DrawProperty(matEditor, matSender, matProps, propName, propIsTexture,
                propIsTexture && HasAttribute(propAttributes, att_ShowScaleOffset));
                DrawIdentMinus();
            }

            if (!drawDefaultProperty)
                return;

            // Draw default property if is not conditional...

            if (HasAttribute(matSender.shader.GetPropertyAttributes(index), att_Space) ||
                HasAttribute(matSender.shader.GetPropertyAttributes(index), att_Space.ToUpper()))
                DrawS();

            DrawProperty(matEditor, matSender, matProps, propName, propIsTexture,
                propIsTexture && HasAttribute(propAttributes, att_ShowScaleOffset));
        }

        #region Custom Editor Utilities

        // Attribute handling

        private bool HasAttribute(string[] atts, string specificAttribute)
        {
            foreach (string s in atts)
            {
                if (s.Equals(specificAttribute))
                    return true;
            }
            return false;
        }

        private bool GetConditionalPropertyResult(MaterialProperty prop, in string[] valuesToCompare)
        {
            for (int i = 0; i < valuesToCompare.Length; i++)
            {
                switch(prop.type)
                {
                    case MaterialProperty.PropType.Float:
                        if (prop.floatValue.ToString() == valuesToCompare[i])
                            return true;
                        break;

                    case MaterialProperty.PropType.Int:
                        if (prop.intValue.ToString() == valuesToCompare[i])
                            return true;
                        break;

                    case MaterialProperty.PropType.Texture:
                        if (prop.textureValue != null && valuesToCompare[i] == "1")
                            return true;
                        break;
                }
            }
            return false;
        }

        private bool FindConditionalTargetProperty(MaterialProperty[] properties, string targetProp, out MaterialProperty foundProp)
        {
            foundProp = null;
            foreach (MaterialProperty prop in properties)
            {
                if (prop.name == targetProp)
                {
                    foundProp = prop;
                    return true;
                }
            }
            return false;
        }

        private MaterialProperty GetMaterialProp(string propertyName, MaterialProperty[] materialProps, Material materialSender)
        {
            MaterialProperty property = FindProperty(propertyName, materialProps);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' couldn't be found in {materialSender.shader} shader");
                return null;
            }
            return property;
        }

        // Editor shortcuts

        private void DrawProperty(MaterialEditor matSrc, Material matSender, MaterialProperty[] props, string propertyName, bool isTexture = false, bool showTexScaleOffset = true)
        {
            MaterialProperty property = GetMaterialProp(propertyName, props, matSender);

            if (!isTexture)
                matSrc.ShaderProperty(property, new GUIContent(property.displayName));
            else
            {
                Rect last = EditorGUILayout.GetControlRect();
                matSrc.TexturePropertyMiniThumbnail(last, property, property.displayName, "");
                if (showTexScaleOffset)
                {
                    last = EditorGUILayout.GetControlRect();
                    last.height += 25;
                    matSrc.TextureScaleOffsetProperty(last, property);
                    DrawS(40);
                }
            }
        }

        public enum LabelStyle { Default, Bold, Italic };

        private void DrawL(string s, LabelStyle lstyle = LabelStyle.Default)
        {
            switch (lstyle)
            {
                case LabelStyle.Default:
                    GUILayout.Label(s);
                    break;
                case LabelStyle.Bold:
                    GUILayout.Label(s, EditorStyles.boldLabel);
                    break;
                case LabelStyle.Italic:
                    GUIStyle st = new GUIStyle(EditorStyles.miniBoldLabel);
                    st.fontStyle = FontStyle.Italic;
                    GUILayout.Label(s, st);
                    break;
            }
        }

        private void DrawV(bool box = true)
        {
            if (!box)
                GUILayout.BeginVertical();
            else
                GUILayout.BeginVertical("Box");
        }

        private void DrawVE() => GUILayout.EndVertical();
        
        private void DrawS(int s = 10) => GUILayout.Space(s);
        
        private void DrawIdentPlus() => EditorGUI.indentLevel++;
        
        private void DrawIdentMinus() => EditorGUI.indentLevel--;
        
        #endregion
    }
}