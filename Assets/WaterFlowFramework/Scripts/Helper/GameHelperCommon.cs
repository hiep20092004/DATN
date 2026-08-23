using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using WaterFlow.Enums;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ObjectPooling;
using TMPro;

namespace WaterFlow.Framework.Helper
{
    public static class GameHelperCommon
    {
        // public static GameResourceType ResourceType(this GameResource resource)
        // {
        //     switch (resource)
        //     {
        //         case GameResource.Coin:
        //         case GameResource.Lives:
        //             return GameResourceType.Currency;
        //     }
        //
        //     return GameResourceType.None;
        // }

        public static void ReturnObject(this IPoolingObject poolingObject)
        {
            GameSystem.GetService<DefaultPoolingService>().ReturnObj(poolingObject);
        }

        public static TweenerCore<int, int, NoOptions> DOCounterFormat(
            this TMP_Text target, int fromValue, int endValue, string format, float duration)
        {
            int v = fromValue;
            TweenerCore<int, int, NoOptions> t = DOTween.To(() => v, x =>
            {
                v = x;
                target.text = string.Format(format, v);
            }, endValue, duration);
            t.SetTarget(target);
            return t;
        }

        // No localization plugin in this project: terms fall back to a readable split of the key.
        public static void SetLocalize(this TMP_Text target, string term)
        {
            target.text = SplitByUppercase(term);
        }

        public static void SetLocalizeParam(this TMP_Text target, string param, string value)
        {
            target.text = param.Replace("{[VALUE]}", value);
        }

        public static string SplitByUppercase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input, "(?<!^)([A-Z])", " $1");
        }
    }
}