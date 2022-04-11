using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1, level2;

            GetLevels(doc, out level1, out level2);
            CreateBuilding(doc, level1, level2);


            /* ВОТ ЭТО ВСЕ НИЖЕ ПИСАЛИ КАК ПРИМЕР, ЧТОБЫ РАЗОБРАТЬ ФИЛЬТРЫ ДЛЯ СИСТЕМНЫХ И ЗАГРУЖАЕМЫХ СЕМЕЙСТВ
             
            var result1 = new FilteredElementCollector(doc) //ищем все стены в документе. Wall - это системное семейство, достаточно только фильтрации по классу
                .OfClass(typeof(Wall)) //ищем все объекты класса Wall (в объектно-ориентированном представлении).
                                       //Такую строку можно и убрать, т.к. ниже на 2 строки мы фильтруем через тип, но через тип - это linq фильтрация, а она ГОРАЗДО медленнее, чем фильтрация Revit
                                       //поэтому быстрее именно в таком виде
                //.Cast<Wall>() - это преобразование объектов в класс стена, так лучше не делать, т.к. при попадании в список чего-то кроме стены - будет исключение. Лучше см. следующую строку
                .OfType<Wall>() //выполняет фильтрацию на основе заданного типа, а не преобразование то есть выберет только стены
                .ToList();

            var result2 = new FilteredElementCollector(doc) //ищем все двери 
                .OfClass(typeof(FamilyInstance)) //ищем все объекты класса FamilyInstance
                .OfCategory(BuiltInCategory.OST_Doors) //добавляем фильтр по категории Doors, т.к. Doors - это загружаемое, а не системное семейство
                .OfType<FamilyInstance>() //отфиьтровали только FamilyInstance
                //.Where(x=>x.Name.Equals("0915 x 2134 мм") - это если мы хотим прямо конкретный типоразмер двери отфильтровать; вместо Equals можно было использовать ==
                .ToList();

            var result3 = new FilteredElementCollector(doc) // 
               .WhereElementIsNotElementType() 
               .ToList();
            */

            return Result.Succeeded;
        }

        private static void GetLevels (Document doc, out Level level1, out Level level2)
        {
            List<Level> listLevel = new FilteredElementCollector(doc) //отфильтруем все уровни документа в список
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            level1 = listLevel
                .Where(x=>x.Name.Equals("Уровень 1"))
                .FirstOrDefault(); //это чтобы взять нужный нам только 1 уровень, из коллекции
            level2 = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault(); //это чтобы взять нужный нам только 2 уровень, из коллекции
        }

        private static void CreateBuilding(Document doc, Level level1, Level level2)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters); //зададим ширину дома, приведя ее от 10000мм к системным еденицам
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters); //зададим глубину дома, приведя ее от 5000мм к системным еденицам
            double dx = width / 2;
            double dy = depth / 2; //получать координаты мы решили от центра нашего дома

            List<XYZ> points = new List<XYZ>(); //создаем список координат (помним, что координаты от центра дома)
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0)); //добавили пятую точку ровно аналогичную первой для простоты, т.к. строить стены мы будем в цикле, перебирая точки попарно: 1-2, 2-3, 3-4, 4-5

            List<Wall> walls = new List<Wall>(); //создали массив под стены на будущее

            Transaction tr = new Transaction(doc); //запускаем транзакцию для добавления в модель
            tr.Start("Построение здания");
            for (int i = 0; i < 4; i++) //перебираем циклом точки
            {
                Line line = Line.CreateBound(points[i], points[i + 1]); //на основании точек создаем линию
                Wall wall = Wall.Create(doc, line, level1.Id, false); // по линии создаем стену. !!! использовали .Id
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id); //задаем высоту для каждой стены через отдельный параметр
                walls.Add(wall); //добавляем стену в массив на будущее                
            }

            AddDoor(doc, level1, walls[0]); //добавляем дверь методом в первую стену из списка

            for (int i = 0; i < 3; i++)
            {
                AddWindow(doc, level1, walls[i+1]);
            }

            AddRoof(doc, level2, walls); //добавляем метод для создания крыши

            tr.Commit();
        }

        
        private static void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc) //отфильтровываем из документа необходимое семейство и типоразмер двери
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve; // Location для стены - это кривая Curve, поэтому преобразуем ее к этому типу
            XYZ startPoint = hostCurve.Curve.GetEndPoint(0); //находим точку начала кривой, где 0 - самая первая точка
            XYZ endPoint = hostCurve.Curve.GetEndPoint(1); //находим точку конца кривой
            XYZ centerPoint = (startPoint + endPoint) / 2; //находим центр кривой, совпадает с центром проекции стены

            if (!doorType.IsActive)
                doorType.Activate(); //такую запись мы делаем по подсказку Jeremy Tammick т.к. необходимо проверить активен ли такой тип втсавляемго элемента
                                     //в документе и если нет, то активировать

            doc.Create.NewFamilyInstance(centerPoint, doorType, wall, level1, StructuralType.NonStructural); //создаем экземпляр двери в модели
        }

        private static void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc) //отфильтровываем из документа необходимое семейство и типоразмер двери
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0610 x 1830 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve; //'это бы тоже в отдельный метод вынести
            XYZ startPoint = hostCurve.Curve.GetEndPoint(0); 
            XYZ endPoint = hostCurve.Curve.GetEndPoint(1); 
            XYZ centerPoint = (startPoint + endPoint) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            Element window = doc.Create.NewFamilyInstance(centerPoint, windowType, wall, level1, StructuralType.NonStructural); //создаем экземпляр окна в модели
            Parameter height = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            double heightMM = UnitUtils.ConvertToInternalUnits(850, UnitTypeId.Millimeters); //где 850 - высота от пола до низа окна
            height.Set(heightMM); //делаем все это чтобы окна были подняты от пола
        }

        private static void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc) //выбираем тип крыши, которую будем создавать
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(-20, -10, 13), new XYZ(-20, 0, 20)));
            curveArray.Append(Line.CreateBound(new XYZ(-20, 0, 20), new XYZ(-20, 10, 13)));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, -19, 19);

            // ЭТО ПРИМЕР КОДА ИЗ ЛЕКЦИИ ДЛЯ СОЗДАНИЯ КРЫШИ ЧЕРЕЗ FootPrintRoof
            /*
            double wallWidth = walls[0].Width; //возьмем ширину стены
            double halfWidth = wallWidth / 2; //это половина толщины стены (делаем чтобы край крыши был не по середине стены, а по краю стены)
            
            List<XYZ> roofPoints = new List<XYZ>(); //создаем список координат для смещения углов крыши от центра стен
            roofPoints.Add(new XYZ(-halfWidth, -halfWidth, 0));
            roofPoints.Add(new XYZ(halfWidth, -halfWidth, 0)); 
            roofPoints.Add(new XYZ(halfWidth, halfWidth, 0));
            roofPoints.Add(new XYZ(-halfWidth, halfWidth, 0));
            roofPoints.Add(new XYZ(-halfWidth, -halfWidth, 0)); //добавляем каждую точку смещения

            Application app = doc.Application;

            CurveArray footprint = app.Create.NewCurveArray(); //создаем массив кривых, которые образуют стены, которые будут формировать контур крыши
            for (int i = 0; i < 4; i++) //переберем стены и запихнем в массив кривые
            {
                LocationCurve curve = walls[i].Location as LocationCurve;
                XYZ startPoint = curve.Curve.GetEndPoint(0);
                XYZ endPoint = curve.Curve.GetEndPoint(1); //берем точки начала и конца отрезка кривой
                Line line = Line.CreateBound(startPoint + roofPoints[i], endPoint + roofPoints[i + 1]); //на основе точек создаем новую ЛИНИЮ со смещением по толщине стен
                                                                                                //
                footprint.Append(line); //присоединяем линию в кривую, по которой будет строиться кровля
            }
            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray(); //с этим массивом (в котором хранятся все грани крыши) потом можно будет менять свойства крыши
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping); //создаем крышу
                        
            foreach (ModelCurve m in footPrintToModelCurveMapping) //по сути перебираем счетчиком массив с гранями кровли
            {
                footprintRoof.set_DefinesSlope(m, true); //это вроде дает возможность устанавливать уклон?
                footprintRoof.set_SlopeAngle(m, 0.5); //это задает угол наклона граней крыши 0,5 - тангенс угла
            }
            */
        }
    }
}
