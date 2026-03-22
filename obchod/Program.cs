using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static simulace.Zakaznik;

namespace simulace
{

    public enum TypUdalosti
    {
        Start,
        Trpelivost,
        Obslouzen
    }

    public static class Generator
    {
        public static Random rnd = new(12345);
    }
    public class Udalost
    {
        public int kdy;
        public Proces kdo;
        public TypUdalosti co;
        public Udalost(int kdy, Proces kdo, TypUdalosti co)
        {
            this.kdy = kdy;
            this.kdo = kdo;
            this.co = co;

        }
    }
    public class Kalendar
    {
        public List<Udalost> seznam;
        public Kalendar()
        {
            seznam = new List<Udalost>();
        }
        public void Pridej(int kdy, Proces kdo, TypUdalosti co)
        {
            //Console.WriteLine("PLAN: {0} {1} {2}", kdy, kdo.ID, co);
            // pro hledani chyby:
            foreach (Udalost ud in seznam)
                if (ud.kdo == kdo)
                    Console.WriteLine("");


            seznam.Add(new Udalost(kdy, kdo, co));
        }
        public void Odeber(Proces kdo, TypUdalosti co)
        {
            foreach (Udalost ud in seznam)
            {
                if ((ud.kdo == kdo) && (ud.co == co))
                {
                    seznam.Remove(ud);
                    return; // odebiram jen jeden vyskyt!
                }
            }
        }
        public Udalost Prvni()
        {
            Udalost prvni = null;
            foreach (Udalost ud in seznam)
                if ((prvni == null) || (ud.kdy < prvni.kdy))
                    prvni = ud;
            seznam.Remove(prvni);
            return prvni;
        }
        public Udalost Vyber()
        {
            return Prvni();
        }

    }

    public abstract class Proces
    {
        public static char[] mezery = { ' ' };
        public int patro;
        public string ID;
        public abstract void Zpracuj(Udalost ud);
        public void log(string zprava)
        {
            //if (ID == "Dana")
            //if (ID == "elefant")
            //if (this is Zakaznik)
            //Console.WriteLine($"{model.Cas}/{patro} {ID}: {zprava}");
        }
        protected Model model;
    }

    public class Oddeleni : Proces
    {
        public int rychlost;
        public List<Zakaznik> fronta;
        private bool obsluhuje;

        public Oddeleni(Model model, string popis)
        {
            this.model = model;
            string[] popisy = popis.Split(Proces.mezery, StringSplitOptions.RemoveEmptyEntries);
            this.ID = popisy[0];
            this.patro = int.Parse(popisy[1]);
            if (this.patro > model.MaxPatro)
                model.MaxPatro = this.patro;
            this.rychlost = int.Parse(popisy[2]);
            obsluhuje = false;
            fronta = new List<Zakaznik>();
            model.VsechnaOddeleni.Add(this);
        }
        public void ZaradDoFronty(Zakaznik zak)
        {
            fronta.Add(zak);
            log("do fronty " + zak.ID);

            if (obsluhuje) ; // nic
            else
            {
                obsluhuje = true;
                model.Naplanuj(model.Cas, this, TypUdalosti.Start);
            }
        }
        public void VyradZFronty(Zakaznik koho)
        {
            fronta.Remove(koho);
        }
        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (fronta.Count == 0)
                        obsluhuje = false; // a dal neni naplanovana a probudi se tim, ze se nekdo zaradi do fronty
                    else
                    {
                        Zakaznik zak = fronta[0];
                        fronta.RemoveAt(0);
                        model.Odplanuj(zak, TypUdalosti.Trpelivost);
                        model.Naplanuj(model.Cas + rychlost, zak, TypUdalosti.Obslouzen);
                        model.Naplanuj(model.Cas + rychlost, this, TypUdalosti.Start);
                    }
                    break;
            }
        }
    }
    public enum SmeryJizdy
    {
        Nahoru,
        Dolu,
        Stoji
    }
    
    
     public class Pasazer
        {
            public Zakaznik kdo;
            public int kamJede;
            public Pasazer(Zakaznik kdo, int kamJede)
            {
                this.kdo = kdo;
                this.kamJede = kamJede;
            }
        }
    
    
    
    public class Vytah : Proces
    {
        public int kapacita;
        public int dobaNastupu;
        public int dobaVystupu;
        public int dobaPatro2Patro;
        static int[] ismery = { +1, -1, 0 }; // prevod (int) SmeryJizdy na smer
        public bool simulace = false;

       

        public List<Pasazer>[,] cekatele; // [patro,smer]
        private List<Pasazer> naklad; // pasazeri ve vytahu
        private SmeryJizdy smer;
        private int kdyJsemMenilSmer;

        public void PridejDoFronty(int odkud, int kam, Zakaznik kdo)
        {
            Pasazer pas = new Pasazer(kdo, kam);
            if (kam > odkud)
                this.cekatele[odkud, (int)SmeryJizdy.Nahoru].Add(pas);
            else
                this.cekatele[odkud, (int)SmeryJizdy.Dolu].Add(pas);

            // pripadne rozjet stojici vytah:
            if (smer == SmeryJizdy.Stoji)
            {
                model.Odplanuj(model.vytah, TypUdalosti.Start); // kdyby nahodou uz byl naplanovany
                model.Naplanuj(model.Cas, this, TypUdalosti.Start);
            }
        }
        public bool CekaNekdoVPatrechVeSmeruJizdy()
        {
            int ismer = ismery[(int)smer];
            for (int pat = patro + ismer; (pat > 0) && (pat <= model.MaxPatro); pat += ismer)
                if ((cekatele[pat, (int)SmeryJizdy.Nahoru].Count > 0) || (cekatele[pat, (int)SmeryJizdy.Dolu].Count > 0))
                {
                    if (cekatele[pat, (int)SmeryJizdy.Nahoru].Count > 0)
                        log("Nahoru čeká " + cekatele[pat, (int)SmeryJizdy.Nahoru][0].kdo.ID
                        + " v patře " + pat + "/" + cekatele[pat, (int)SmeryJizdy.Nahoru][0].kdo.patro);
                    if (cekatele[pat, (int)SmeryJizdy.Dolu].Count > 0)
                        log("Dolů čeká " + cekatele[pat, (int)SmeryJizdy.Dolu][0].kdo.ID
                        + " v patře " + pat + "/" + cekatele[pat, (int)SmeryJizdy.Dolu][0].kdo.patro);

                    //log(" x "+cekatele[pat, (int)SmeryJizdy.Nahoru].Count+" x "+cekatele[pat, (int)SmeryJizdy.Dolu].Count);
                    return true;
                }
            return false;
        }

        public Vytah(Model model, string popis)

        {
            this.model = model;
            string[] popisy = popis.Split(Proces.mezery, StringSplitOptions.RemoveEmptyEntries);
            this.ID = popisy[0];
            this.kapacita = int.Parse(popisy[1]);
            this.dobaNastupu = int.Parse(popisy[2]);
            this.dobaVystupu = int.Parse(popisy[3]);
            this.dobaPatro2Patro = int.Parse(popisy[4]);
            this.patro = 0;
            this.smer = SmeryJizdy.Stoji;
            this.kdyJsemMenilSmer = -1;

            cekatele = new List<Pasazer>[model.MaxPatro + 1, 2];
            for (int i = 0; i < model.MaxPatro + 1; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    cekatele[i, j] = new List<Pasazer>();
                }

            }
            naklad = new List<Pasazer>();
        }

        public Vytah() { }
        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:

                    // HACK pro cerstve probuzeny vytah:
                    if (smer == SmeryJizdy.Stoji)
                        // stoji, tedy nikoho neveze a nekdo ho prave probudil => nastavim jakykoliv smer a najde ho:
                        smer = SmeryJizdy.Nahoru;

                    // chce nekdo vystoupit?
                    foreach (Pasazer pas in naklad)
                        if (pas.kamJede == patro)
                        // bude vystupovat:
                        {
                            naklad.Remove(pas);

                            pas.kdo.patro = patro;
                            //if(!this.simulace)
                            model.Naplanuj(model.Cas + dobaVystupu, pas.kdo, TypUdalosti.Start);
                            log("vystupuje " + pas.kdo.ID);


                            model.Naplanuj(model.Cas + dobaVystupu, this, TypUdalosti.Start);
                            if (this.simulace) pas.kdo.vystoupil = true;

                            return; // to je pro tuhle chvili vsechno
                        }

                    // muze a chce nekdo nastoupit?
                    if (naklad.Count == kapacita)
                    // i kdyby chtel nekdo nastupovat, nemuze; veze lidi => pokracuje:
                    {
                        // popojet:
                        int ismer = ismery[(int)smer];
                        patro = patro + ismer;

                        string spas = "";
                        foreach (Pasazer pas in naklad)
                            spas += " " + pas.kdo.ID;
                        log("odjíždím");
                        model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                        return; // to je pro tuhle chvili vsechno
                    }
                    else
                    // neni uplne plny
                    {
                        // chce nastoupit nekdo VE SMERU jizdy?
                        if (cekatele[patro, (int)smer].Count > 0)
                        {
                            log("nastupuje " + cekatele[patro, (int)smer][0].kdo.ID);
                            naklad.Add(cekatele[patro, (int)smer][0]);
                            cekatele[patro, (int)smer].RemoveAt(0);
                            model.Naplanuj(model.Cas + dobaNastupu, this, TypUdalosti.Start);

                            return; // to je pro tuhle chvili vsechno
                        }

                        // ve smeru jizdy nikdo nenastupuje:
                        if (naklad.Count > 0)
                        // nikdo nenastupuje, vezu pasazery => pokracuju v jizde:
                        {
                            // popojet:
                            int ismer = ismery[(int)smer];
                            patro = patro + ismer;

                            string spas = "";
                            foreach (Pasazer pas in naklad)
                                spas += " " + pas.kdo.ID;
                            //log("nekoho vezu");
                            log("odjíždím: " + spas);

                            model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                            return; // to je pro tuhle chvili vsechno
                        }

                        // vytah je prazdny, pokud v dalsich patrech ve smeru jizdy uz nikdo neceka, muze zmenit smer nebo se zastavit:
                        if (CekaNekdoVPatrechVeSmeruJizdy() == true)
                        // pokracuje v jizde:
                        {
                            // popojet:
                            int ismer = ismery[(int)smer];
                            patro = patro + ismer;

                            //log("nekdo ceka");
                            log("odjíždím");
                            model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                            return; // to je pro tuhle chvili vsechno
                        }

                        // ve smeru jizdy uz nikdo neceka => zmenit smer nebo zastavit:
                        if (smer == SmeryJizdy.Nahoru)
                            smer = SmeryJizdy.Dolu;
                        else
                            smer = SmeryJizdy.Nahoru;

                        log("změna směru");

                        //chce nekdo nastoupit prave tady?
                        if (kdyJsemMenilSmer != model.Cas)
                        {
                            kdyJsemMenilSmer = model.Cas;
                            // podivat se, jestli nekdo nechce nastoupit opacnym smerem:
                            model.Naplanuj(model.Cas+1, this, TypUdalosti.Start);
                            return;
                        }

                        // uz jsem jednou smer menil a zase nikdo nenastoupil a nechce => zastavit
                        log("zastavuje");
                        smer = SmeryJizdy.Stoji;
                        return; // to je pro tuhle chvili vsechno
                    }
            }
        }
        public int ZaJakDlouhoDojede(int start, int cil)
        {
            int cas = this.model.Cas;
            Model podmodel = new Model();
            Vytah podvytah = new Vytah();
            podvytah.simulace = true;
            podmodel.Cas = this.model.Cas;
            podmodel.vytah = podvytah;
            podmodel.kalendar = new Kalendar();
            //podmodel.VsechnaOddeleni = this.model.VsechnaOddeleni;
            foreach(Udalost udalost in this.model.kalendar.seznam)
            {
                if (udalost.kdo is Vytah)
                {
                    Udalost nova_udalost = new Udalost(udalost.kdy, podvytah, udalost.co);
                    podmodel.kalendar.seznam.Add(nova_udalost);
                }
            }
            podvytah.model = podmodel;
            podvytah.kapacita = this.kapacita;
            podvytah.dobaNastupu = this.dobaNastupu;
            podvytah.dobaVystupu = this.dobaVystupu;
            podvytah.dobaPatro2Patro = this.dobaPatro2Patro;
            podvytah.patro = this.patro;
            podvytah.smer = this.smer;
            podvytah.kdyJsemMenilSmer = this.kdyJsemMenilSmer;
            podvytah.naklad = new List<Pasazer>();
            foreach (Pasazer pas in this.model.vytah.naklad)
            {
                podvytah.naklad.Add(new Pasazer(new Zakaznik(podmodel), pas.kamJede));
            }
            podvytah.cekatele = new List<Pasazer>[this.model.vytah.cekatele.GetLength(0), this.model.vytah.cekatele.GetLength(1)];
            for (int i = 0; i < podvytah.cekatele.GetLength(0); i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    podvytah.cekatele[i, j] = new List<Pasazer>(); 
                }
            }
            for (int i = 0; i < this.model.vytah.cekatele.GetLength(0); i++)
            {
                for (int j = 0; j < this.model.vytah.cekatele.GetLength(1); j++)
                {
                    List<Pasazer> list = this.model.vytah.cekatele[i, j];
                    foreach (Pasazer pas in list)
                    {
                        Pasazer novypasazer = new Pasazer(new Zakaznik(podmodel), pas.kamJede);
                        podvytah.cekatele[i, j].Add(novypasazer);
                    }
                }

            }
            Zakaznik sledovany = new Zakaznik(podmodel);
            podvytah.PridejDoFronty(start, cil, sledovany);
            Udalost ud;
            while ((ud = podmodel.kalendar.Vyber()) != null)
            {
                podmodel.Cas = ud.kdy;
                if (ud.kdo is Vytah)
                ud.kdo.Zpracuj(ud);
                if (ud.kdo == sledovany)
                {
                    if (sledovany.vystoupil) return podmodel.Cas - cas;
                }
                //Console.WriteLine("{0} {1} {2}", ud.kdy, ud.kdo.ID, ud.co);
            }
            return 0;
        }
    }

    



    public class Zakaznik : Proces
    {
        public int trpelivost;
        public int prichod;
        public List<Oddeleni> Nakupy;
        public Zakaznik(Model model)
        {
            this.model = model;
            this.prichod = Generator.rnd.Next(0, 601);
            this.trpelivost = Generator.rnd.Next(1, 181);
            Nakupy = new List<Oddeleni>();
            int pocet_nakupu = Generator.rnd.Next(1, 21);
            if (!this.model.vytah.simulace)
            {
                for (int i = 0; i < pocet_nakupu; i++)
                {
                    int j = Generator.rnd.Next(1, model.VsechnaOddeleni.Count);
                    Nakupy.Add(model.VsechnaOddeleni[j]);
                }
            }
            this.patro = 0;
            //Console.WriteLine("Init Zakaznik: {0}", ID);
            if(!this.model.vytah.simulace)
            model.Naplanuj(prichod, this, TypUdalosti.Start);
        }
        public bool vystoupil = false;
        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (Nakupy.Count == 0)
                    // ma nakoupeno
                    {
                        if (patro == 0)
                            log("-------------- odchází"); // nic, konci
                        else
                            model.vytah.PridejDoFronty(patro, 0, this);
                    }
                    else
                    {
                        Oddeleni odd = Nakupy[0];
                        int pat = odd.patro;
                        if (pat == patro) // to oddeleni je v patre, kde prave jsem
                        {
                            if (Nakupy.Count > 1)
                                model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                            odd.ZaradDoFronty(this);
                        }
                        else
                            model.vytah.PridejDoFronty(patro, pat, this);
                    }
                    break;
                case TypUdalosti.Obslouzen:
                    //log("Nakoupeno: " + Nakupy[0]);
                    if(!this.model.vytah.simulace)
                    Nakupy.RemoveAt(0);
                    // ...a budu hledat dalsi nakup -->> Start
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                case TypUdalosti.Trpelivost:
                    log("!!! Trpělivost: " + Nakupy[0]);
                    // vyradit z fronty:
                    {
                        Oddeleni odd = Nakupy[0];
                        odd.VyradZFronty(this);
                    }

                    // prehodit tenhle nakup na konec:
                    Oddeleni nesplneny = Nakupy[0];
                    if (!(this.model.vytah.simulace))
                    {
                        Nakupy.RemoveAt(0);
                        Nakupy.Add(nesplneny);
                    }
                    // ...a budu hledat dalsi nakup -->> Start
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
            }
        }

        public class SuperZakaznik_1 : Zakaznik
        {
            public SuperZakaznik_1(Model model) : base(model)
            { }

            public Oddeleni PrednostniNakup(List<Oddeleni> Nakupy)
            {
                Oddeleni odd;
                for(int i = 0; i<Nakupy.Count; i++)
                {
                    if (Nakupy[i].patro == patro)
                    {
                        odd = Nakupy[i];
                        Nakupy[i] = Nakupy[0];
                        Nakupy[0] = odd;
                        return odd;
                    }
                }
                return Nakupy[0];
            }
            public override void Zpracuj(Udalost ud)
            {
                switch (ud.co)
                {
                    case TypUdalosti.Start:
                        if (Nakupy.Count == 0)
                        // ma nakoupeno
                        {
                            if (patro == 0)
                                log("-------------- odchází"); // nic, konci
                            else
                                model.vytah.PridejDoFronty(patro, 0, this);
                        }
                        else
                        {
                            Oddeleni odd = PrednostniNakup(Nakupy);
                            int pat = odd.patro;
                            if (pat == patro) // to oddeleni je v patre, kde prave jsem
                            {
                                if (Nakupy.Count > 1)
                                    model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                                odd.ZaradDoFronty(this);
                            }
                            else
                                model.vytah.PridejDoFronty(patro, pat, this);
                        }
                        break;
                    case TypUdalosti.Obslouzen:
                        //log("Nakoupeno: " + Nakupy[0]);
                        if(!(this.model.vytah.simulace))
                        Nakupy.RemoveAt(0);
                        // ...a budu hledat dalsi nakup -->> Start
                        model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                        break;
                    case TypUdalosti.Trpelivost:
                        log("!!! Trpělivost: " + Nakupy[0]);
                        // vyradit z fronty:
                        {
                            Oddeleni odd = Nakupy[0];
                            odd.VyradZFronty(this);
                        }

                        // prehodit tenhle nakup na konec:
                        Oddeleni nesplneny = Nakupy[0];
                        if (!(this.model.vytah.simulace))
                        {
                            Nakupy.RemoveAt(0);
                            Nakupy.Add(nesplneny);
                        }

                        // ...a budu hledat dalsi nakup -->> Start
                        model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                        break;
                }
            }
        }

        private Oddeleni OddeleniPodleJmena(string kamChci)
        {
            foreach (Oddeleni odd in model.VsechnaOddeleni)
                if (odd.ID == kamChci)
                    return odd;
            return null;
        }
    }
    public class SuperZakaznik_2 : Zakaznik
    {
        public SuperZakaznik_2(Model model) : base(model)
        { }

        public int fronta(Oddeleni odd)
        {
            int kamsmer;
            List<Pasazer> fronta;
            if (odd.patro == patro) return odd.fronta.Count;
            else
            {
                if (odd.patro > patro)
                    kamsmer = 0;
                else kamsmer = 1;
                fronta = this.model.vytah.cekatele[patro, kamsmer];
                int frontaVytahu = fronta.Count ;
                int frontaOddeleni = odd.fronta.Count;
                return frontaVytahu + frontaOddeleni;

            }

        }
        public Oddeleni NejvyhodnejsiOddeleni(List<Oddeleni> Nakupy)
        {
            Oddeleni nejvyhodnejsi = Nakupy[0];
            int nejrychlost = int.MaxValue;
            int rychlost;
            int index = 0;
            for (int i = 1; i < Nakupy.Count; i++)
            {
                rychlost = fronta(Nakupy[i]);
                if (rychlost < nejrychlost)
                {
                    nejrychlost = rychlost;
                    nejvyhodnejsi = Nakupy[i];
                    index = i;
                }

            }
            Nakupy[index] = Nakupy[0];
            Nakupy[0] = nejvyhodnejsi;
            return nejvyhodnejsi;
        }

        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (Nakupy.Count == 0)
                    // ma nakoupeno
                    {
                        if (patro == 0)
                            log("-------------- odchází"); // nic, konci
                        else
                            model.vytah.PridejDoFronty(patro, 0, this);
                    }
                    else
                    {
                        Oddeleni odd = NejvyhodnejsiOddeleni(Nakupy);
                        int pat = odd.patro;
                        if (pat == patro) // to oddeleni je v patre, kde prave jsem
                        {
                            if (Nakupy.Count > 1)
                                model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                            odd.ZaradDoFronty(this);
                        }
                        else
                            model.vytah.PridejDoFronty(patro, pat, this);
                    }
                    break;
                case TypUdalosti.Obslouzen:
                    log("Nakoupeno: " + Nakupy[0]);
                    if(!(this.model.vytah.simulace))
                    Nakupy.RemoveAt(0);
                    // ...a budu hledat dalsi nakup -->> Start
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                case TypUdalosti.Trpelivost:
                    log("!!! Trpělivost: " + Nakupy[0]);
                    // vyradit z fronty:
                    {
                        Oddeleni odd = Nakupy[0];
                        odd.VyradZFronty(this);
                    }

                    // prehodit tenhle nakup na konec:
                    Oddeleni nesplneny = Nakupy[0];
                    if (!(this.model.vytah.simulace))
                    {
                        Nakupy.RemoveAt(0);
                        Nakupy.Add(nesplneny);
                    }

                    // ...a budu hledat dalsi nakup -->> Start
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                }
            }
        }

    public class Model
    {
        public int Cas;
        public Vytah vytah;
        public Dictionary<Zakaznik, int> zakaznici = new Dictionary<Zakaznik, int>();
        public List<Oddeleni> VsechnaOddeleni = new List<Oddeleni>();
        public int MaxPatro;
        public Kalendar kalendar;
        public void Naplanuj(int kdy, Proces kdo, TypUdalosti co)
        {
            kalendar.Pridej(kdy, kdo, co);
        }
        public void Odplanuj(Proces kdo, TypUdalosti co)
        {
            kalendar.Odeber(kdo, co);
        }



        public void VytvorProcesy(int pocet_zakazniku)
        {
            System.IO.StreamReader soubor = new System.IO.StreamReader("obchod_data.txt");
            while (!soubor.EndOfStream)
            {
                string s = soubor.ReadLine();
                if (s != "")
                {
                    switch (s[0])
                    {
                        case 'O': new Oddeleni(this, s.Substring(1)); break;
                        case 'V': vytah = new Vytah(this, s.Substring(1)); break;
                    }
                }
            }

            for (int i = 0; i < pocet_zakazniku; i++)
            {
                Zakaznik zakaznik = null;
                switch (i % 3)
                {
                    case 1:
                        zakaznik = new Zakaznik(this); break;
                    case 2:
                        zakaznik = new SuperZakaznik_1(this); break;
                    case 0:
                        zakaznik = new SuperZakaznik_2(this); break;
                }
                zakaznici.Add(zakaznik, 0);

            }

            soubor.Close();
        }
        public int Vypocet(int pocet_zakazniku)
        {
            Cas = 0;
            kalendar = new Kalendar();
            VytvorProcesy(pocet_zakazniku);
            Udalost ud;
            while ((ud = kalendar.Vyber()) != null)
            {
                Cas = ud.kdy;
                ud.kdo.Zpracuj(ud);
                if (ud.kdo is Zakaznik z) zakaznici[z] = Cas - z.prichod;
            }
            int sum0 = 0;
            int sum1 = 0;
            int sum2 = 0;
            int amount0 = 0;
            int amount1 = 0;
            int amount2 = 0;
            foreach (Zakaznik z in zakaznici.Keys)
            {
                if (z is SuperZakaznik_1) { sum1 += zakaznici[z]; amount1++; }
                else if (z is SuperZakaznik_2) { sum2 += zakaznici[z]; amount2++; }
                else { sum0 += zakaznici[z]; amount0++; }
            }
            Console.WriteLine("\n" + pocet_zakazniku + "  " + (amount0 > 0 ? sum0 / amount0 : 0) + "  " + (amount1 > 0 ? sum1 / amount1 : 0) + "  " + (amount2 > 0 ? sum2 / amount2 : 0));
            return Cas;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Model model = new Model();
            for (int pocet = 1; pocet < 502; pocet += 10)
            {
                model.Vypocet(pocet);

            }
            Console.ReadLine();



        }
    }
}