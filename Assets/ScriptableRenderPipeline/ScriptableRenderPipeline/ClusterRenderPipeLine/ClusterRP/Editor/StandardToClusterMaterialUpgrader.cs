using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    public class StandardToClusterMaterialUpgrader
    {
        [MenuItem("RenderPipeline/Cluster Pipeline/Material Upgraders/Upgrade Project Materials", false, 1)]
        private static void UpgradeMaterialsToLDProject()
        {
            List<MaterialUpgrader> upgraders = new List<MaterialUpgrader>();
            GetUpgraders(ref upgraders);

            MaterialUpgrader.UpgradeProjectFolder(upgraders, "Upgrade to Render Graph Pipeline Materials");
        }

        [MenuItem("RenderPipeline/Cluster Pipeline/Material Upgraders/Upgrade Selected Materials", false, 2)]
        private static void UpgradeMaterialsToLDSelection()
        {
            List<MaterialUpgrader> upgraders = new List<MaterialUpgrader>();
            GetUpgraders(ref upgraders);

            MaterialUpgrader.UpgradeSelection(upgraders, "Upgrade to Render Graph Pipeline Materials");
        }

        private static void GetUpgraders(ref List<MaterialUpgrader> upgraders)
        {
            //upgraders.Add(new StandardUpgrader("Standard (Specular setup)"));
            upgraders.Add(new StandardUpgrader("Standard"));
            upgraders.Add(new StandardUpgrader("Standard (Specular setup)"));
            upgraders.Add(new StandardUpgrader("Banzai/ClusterShading/Forward"));
            upgraders.Add(new VegetationUpgrader("Nature/SpeedTree"));
            upgraders.Add(new VegetationUpgrader("Banzai/VegetationPBR"));
        }
    }
}
