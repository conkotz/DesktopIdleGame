using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastProbe : MonoBehaviour
{
    private PointerEventData _ped;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>(64);

    private void Update()
    {
        if (!EventSystem.current) return;

        if (_ped == null)
            _ped = new PointerEventData(EventSystem.current);

        _ped.position = Input.mousePosition;

        _hits.Clear();
        EventSystem.current.RaycastAll(_ped, _hits);

        if (_hits.Count == 0) return;

        int n = Mathf.Min(10, _hits.Count);
        string s = "[UIProbe] ";
        for (int i = 0; i < n; i++)
        {
            var go = _hits[i].gameObject;
            s += $"{i}:{go.name}({go.transform.GetComponentInParent<Canvas>()?.name})  ";
        }
        Debug.Log(s);
    }
}
