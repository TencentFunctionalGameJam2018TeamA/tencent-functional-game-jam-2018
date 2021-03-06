﻿using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{

    public static Character instance;

    /* Config Propertys */
    public GameObject spriteMeshes;
    public Animator animator;
    new public Rigidbody2D rigidbody;
    public HingeJoint2D footHingeJoint;
    public Word startWord;
    public WordHolder wordHolder;
    public GroundCheck groundCheck;
    public Vector2 jumpLength;
    public float maximumAeriallyMovementSpeed = 2; // 空中水平移動最大速度
    public float aeriallyMovementAcceleration = 1; // 空中水平移動加速度
    public float flyMaxSpeed = 2;
    public float flyAddSpeed = 1;
    public StageMask[] stageMasks;
    public StageMask[] finalStageMasks;
    public SpriteLightning[] spriteLightnings;
    public Vector2 startPosition;
    public Animator lightEffectAnimator, combineEffectAnimator, thunderEffectAnimator, deadEffectAnimator;
    public Animator level1PoetryAnimator;
    public Animator hintY, hintX;
    public float skill1DelayTime = 0.3f;
    public float skill4DelayTime = 1f;
    public Cinemachine.CinemachineVirtualCamera cinemachine;

    /* Runtime Propertys */
    bool m_IsUseX;
    GameData m_GameData;
    List<WordGiver> m_TouchingWords = new List<WordGiver>();
    List<StageTrigger> m_EventTrigger = new List<StageTrigger>();
    float m_LastUseSkillTime;
    Cinemachine.CinemachineBasicMultiChannelPerlin perlin;

    /* Getter/Setter Propertys */
    public WordGiver touchingWordGiver
    {
        get
        {
            m_TouchingWords = m_TouchingWords.FindAll(word => !!word);
            foreach (WordGiver word in m_TouchingWords)
                if (word != wordHolder.current)
                    return word;
            return null;
        }
    }

    /* Unity Events */
    void Awake()
    {
        instance = this;

        perlin = cinemachine.GetCinemachineComponent<Cinemachine.CinemachineBasicMultiChannelPerlin>();
        startPosition = transform.position;
        m_GameData = Resources.Load<GameData>("GameData");
        foreach (var stageMask in stageMasks)
            stageMask.gameObject.SetActive(true);
        ResetWord();
    }
    void Update()
    {
        UpdateHorizontal();
        UpdateInputButton();
        UpdateGrounded();
        UpdateState();
        UpdateSpeedY();
        UpdateCameraShake();
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        WordGiver otherWord = other.GetComponent<WordGiver>();
        if (otherWord)
            m_TouchingWords.Add(otherWord);

        StageTrigger stageTrigger = other.GetComponent<StageTrigger>();
        if (stageTrigger)
        {
            if (stageTrigger.id == "level_3_enter")
            {
                foreach (var lightning in spriteLightnings)
                    lightning.StartLightning();
                stageTrigger.gameObject.SetActive(false);
            }
            else if (stageTrigger.id == "hint_y")
            {
                if (wordHolder.current.name == "一")
                    hintY.Play("Show");
            }
            else if (stageTrigger.id == "hint_x")
            {
                if (!m_IsUseX && wordHolder.current.name == "日")
                    hintX.Play("Show");
            }
            else
            {
                m_EventTrigger.Add(stageTrigger);
            }
        }

        CheckPoint checkPoint = other.GetComponent<CheckPoint>();
        if (checkPoint)
        {
            checkPoint.gameObject.SetActive(false);
            startPosition = checkPoint.transform.position;
        }

        DeadArea deadArea = other.GetComponent<DeadArea>();
        if (deadArea)
            CheckGameOver.instance.GameOver();

        Wind wind = other.GetComponent<Wind>();
        if (wind)
        {
            GameObject ag = GameObject.FindWithTag("AudioController");
            AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
            ac.PlayFx("wind");
            ResetWord();
        }


        ThunderWord thunderWord = other.GetComponent<ThunderWord>();
        if (thunderWord)
        {
            if (wordHolder.current.name != "雨" && wordHolder.current.name != "雳")
            {
                thunderWord.Protect();
                CheckGameOver.instance.GameOver();
            }
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        WordGiver otherWord = other.GetComponent<WordGiver>();
        if (otherWord && m_TouchingWords.Contains(otherWord))
            m_TouchingWords.Remove(otherWord);

        StageTrigger stageTrigger = other.GetComponent<StageTrigger>();
        if (stageTrigger)
        {
            if (stageTrigger.id == "hint_y")
            {
                if (hintY.GetCurrentAnimatorStateInfo(0).IsName("Show"))
                    hintY.Play("Hide");
            }
            if (stageTrigger.id == "hint_x")
            {
                if (hintX.GetCurrentAnimatorStateInfo(0).IsName("Show"))
                    hintX.Play("Hide");
            }
            else
            {
                m_EventTrigger.Remove(stageTrigger);
            }
        }
    }

    /* Animation Events */
    void OnJump()
    {
        animator.Play("Falling", 0);
        float vy = Mathf.Sqrt(-2 * Physics2D.gravity.y * Mathf.Abs(jumpLength.y)) * (jumpLength.y < 0 ? -1 : 1);
        float vx = jumpLength.x * Physics2D.gravity.y / (-2 * vy) * (jumpLength.y < 0 ? -1 : 1);
        float rawX = Input.GetAxisRaw("Horizontal");
        vx *= rawX;
        rigidbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse);
    }
    void OnJumpInternal()
    {
        animator.Play("Falling", 0);
        float vy = Mathf.Sqrt(-2 * Physics2D.gravity.y * Mathf.Abs(jumpLength.y)) * (jumpLength.y < 0 ? -1 : 1);
        float vx = 0;
        rigidbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse);
    }

    /* Public */
    public void Restart()
    {
        transform.position = startPosition;
    }

    /* Internal */
    void UpdateInputButton()
    {
        if (Input.GetButtonDown("Y"))
        {
            if (!!touchingWordGiver)
            {
                CombineWords();
                if (hintY.GetCurrentAnimatorStateInfo(0).IsName("Show"))
                    hintY.Play("Hide");
            }
            if (wordHolder.current.name == "日")
            {
                foreach (var stageTrigger in m_EventTrigger)
                {
                    if (stageTrigger.isOn)
                        continue;
                    if (stageTrigger.id == "level_1_light")
                    {
                        stageTrigger.On();
                        foreach (var stageMask in finalStageMasks)
                            stageMask.PlayLightFinal();
                        ResetWord();

                        level1PoetryAnimator.SetInteger("State", level1PoetryAnimator.GetInteger("State") + 1);
                    }
                }
            }
            else if (wordHolder.current.name == "灭")
            {
                foreach (var stageTrigger in m_EventTrigger)
                {
                    if (stageTrigger.isOn)
                        continue;
                    if (stageTrigger.id == "level_1_fire")
                    {
                        GameObject ag = GameObject.FindWithTag("AudioController");
                        AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
                        ac.PutOutFire();
                        stageTrigger.On();
                        FireWall.instance.Close();
                        ResetWord();

                        level1PoetryAnimator.SetInteger("State", level1PoetryAnimator.GetInteger("State") + 1);
                        WindShooter.instance.StartShoot();


                    }
                }
            }
        }
        if (Input.GetButtonDown("B") && wordHolder.current.name != startWord.name)
            ResetWord();
        if (Input.GetButtonDown("X"))
        {
            if (wordHolder.current.name == "日")
            {
                if (Time.time - m_LastUseSkillTime > skill1DelayTime)
                {
                    m_LastUseSkillTime = Time.time;

                    GameObject ag = GameObject.FindWithTag("AudioController");
                    AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
                    ac.PlayFx("shining");
                    m_IsUseX = true;
                    if (hintX.GetCurrentAnimatorStateInfo(0).IsName("Show"))
                        hintX.Play("Hide");

                    foreach (var stageMask in stageMasks)
                        stageMask.PlayLight();
                    lightEffectAnimator.Play("Flash", 0, 0);
                }
            }
            else if (wordHolder.current.name == "灭")
            {
                animator.SetTrigger("PutOutFire");
            }
            else if (wordHolder.current.name == "飞")
            {
                GameObject ag = GameObject.FindWithTag("AudioController");
                AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
                ac.PlayFx("bird");
                animator.SetTrigger("Fly");
            }
            else if (wordHolder.current.name == "雳")
            {
                if (Time.time - m_LastUseSkillTime > skill4DelayTime)
                {
                    m_LastUseSkillTime = Time.time;

                    GameObject ag = GameObject.FindWithTag("AudioController");
                    AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
                    ac.PlayFx("thunder");
                    RocksControl.instance.DestroyRock();
                    thunderEffectAnimator.Play("Flash", 0, 0);

                    perlin.m_AmplitudeGain = 5;
                }
            }
        }
        animator.SetBool("Fly", Input.GetButton("X") && wordHolder.current.name == "飞");
        if (Input.GetButtonDown("A") && groundCheck.isGrounded)
        {
            AnimatorStateInfo currentState = animator.GetNextAnimatorStateInfo(0);
            if (currentState.fullPathHash == 0)
                currentState = animator.GetCurrentAnimatorStateInfo(0);

            if (currentState.IsName("Idle"))
                animator.SetTrigger("Jump");
            else
                OnJumpInternal();
        }
    }
    void CombineWords()
    {
        WordGiver wordGiver = touchingWordGiver;
        if (wordGiver)
        {
            foreach (WordCombine wordCombine in m_GameData.wordCombines)
            {
                List<string> remainWords = wordCombine.combineFromWords.ToList();

                if (!remainWords.Remove(wordGiver.word))
                    continue;
                if (!remainWords.Remove(wordHolder.current.name))
                    continue;

                m_TouchingWords.Remove(wordGiver);

                wordGiver.Take();
                wordHolder.ChangeWord(wordCombine.word);

                GameObject ag = GameObject.FindWithTag("AudioController");
                AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
                ac.LvlUp(wordCombine.word);

                combineEffectAnimator.Play("Flash", 0, 0);
                break;
            }
        }
    }
    void ResetWord()
    {
        if (wordHolder.current && wordHolder.current.name != startWord.name)
        {
            GameObject ag = GameObject.FindWithTag("AudioController");
            AudioController ac = (AudioController)ag.GetComponent(typeof(AudioController));
            ac.LvlDown();
        }
        wordHolder.ChangeWord(startWord);
    }
    void UpdateHorizontal()
    {
        float raw = Input.GetAxis("Horizontal");
        animator.SetInteger("Horizontal", Mathf.Abs(raw) < 0.2f ? 0 : 1);
    }
    void UpdateState()
    {
        AnimatorStateInfo currentState = animator.GetNextAnimatorStateInfo(0);
        if (currentState.fullPathHash == 0)
            currentState = animator.GetCurrentAnimatorStateInfo(0);

        float moveSpeed = 0;
        // float
        animator.speed = 1;
        if (currentState.IsName("Walk"))
        {
            animator.speed = Mathf.Abs(Input.GetAxis("Horizontal"));

            moveSpeed = Input.GetAxis("Horizontal");
            if (moveSpeed != 0)
            {
                transform.localScale = new Vector3(moveSpeed > 0 ? 1 : -1, 1, 1);
                hintY.transform.parent.localScale = transform.localScale;
            }
        }
        else if (currentState.IsName("Falling") || currentState.IsName("Falling_Down") || currentState.IsName("Fly"))
        {
            int rawX = Mathf.RoundToInt(Input.GetAxisRaw("Horizontal"));
            if (rawX != 0)
            {
                transform.localScale = new Vector3(rawX, 1, 1);
                hintY.transform.parent.localScale = transform.localScale;

                if (transform.localScale.x > 0 ? rigidbody.velocity.x < maximumAeriallyMovementSpeed : rigidbody.velocity.x > -maximumAeriallyMovementSpeed)
                    rigidbody.velocity += new Vector2(aeriallyMovementAcceleration * Time.deltaTime * transform.localScale.x, 0);
            }

            if (currentState.IsName("Fly"))
            {
                if (rigidbody.velocity.y < flyMaxSpeed)
                    rigidbody.velocity += new Vector2(0, flyAddSpeed * Time.deltaTime);
            }
        }
        JointMotor2D motor = footHingeJoint.motor;
        motor.motorSpeed = moveSpeed * 600;
        footHingeJoint.motor = motor;
    }
    void UpdateSpeedY()
    {
        animator.SetFloat("vy", rigidbody.velocity.y);
    }
    void UpdateGrounded()
    {
        animator.SetBool("Is Grounded", groundCheck.isGrounded);
    }
    void UpdateCameraShake()
    {
        if (perlin.m_AmplitudeGain > 0)
        {
            perlin.m_AmplitudeGain -= Time.deltaTime * 5;
            perlin.m_AmplitudeGain = Mathf.Max(0, perlin.m_AmplitudeGain);
        }
    }
}
