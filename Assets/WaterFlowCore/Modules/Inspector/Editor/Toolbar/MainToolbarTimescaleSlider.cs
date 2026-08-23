#if UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace WaterFlow.Core
{
    public class MainToolbarTimescaleSlider {
        const float k_minTimeScale = 0f;
        const float k_maxTimeScale = 5f;

        [MainToolbarElement("Timescale/A_Slider", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement TimeSlider() {
            var content = new MainToolbarContent("Time Scale", "Time Scale");
            var slider = new MainToolbarSlider(content, Time.timeScale, k_minTimeScale, k_maxTimeScale, OnSliderValueChanged);
        
            slider.populateContextMenu = (menu) => {
                menu.AppendAction("Reset", _ => {
                    Time.timeScale = 1f;
                    MainToolbar.Refresh("Timescale/A_Slider");
                });
            };
        
            MainToolbarElementStyler.StyleElement<VisualElement>("Timescale/A_Slider", (element) => {
                element.style.paddingLeft = 10f;
                element.style.paddingRight = 2f;
            });
        
            return slider;
        }

        static void OnSliderValueChanged(float newValue) {
            Time.timeScale = newValue;
        }
    }
}

#endif