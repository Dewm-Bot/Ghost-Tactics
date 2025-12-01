using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckEnemies : MonoBehaviour
{
    [Scene]
    public string NextLevel;

    void Update()
    {
        if (GameObject.FindGameObjectsWithTag("Enemy").Length == 0)
        {
            if (!string.IsNullOrEmpty(NextLevel))
            {
                SceneManager.LoadScene(NextLevel);
            }
        }
    }
}