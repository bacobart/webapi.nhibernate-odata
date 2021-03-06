﻿using System;
using System.Linq.Expressions;

namespace Pathoschild.WebApi.NhibernateOdata.Internal
{
	/// <summary>
	/// Intercepts queries before they're parsed by NHibernate to rewrite null check for components which aren't necessary for NHibernate.
	/// NHibernate does not support checking for null on components which have 4 or more properties mapped. I don't know why really, but it crashes
	/// with a recognition error in the HQL.
	/// </summary>
	/// <remarks>
	/// The expression tree generated by the <c>ODataQueryOptions.ApplyTo</c> method looks like the following sample.
	/// <code>
	/// .Call System.Linq.Queryable.Where(
	///        .Constant&lt;NHibernate.Linq.NhQueryable`1[Pathoschild.WebApi.NhibernateOdata.Tests.Models.Parent]&gt;(NHibernate.Linq.NhQueryable`1[Pathoschild.WebApi.NhibernateOdata.Tests.Models.Parent]),
	///        '(.Lambda #Lambda1&lt;System.Func`2[Pathoschild.WebApi.NhibernateOdata.Tests.Models.Parent,System.Boolean]&gt;))
	///
	///    .Lambda #Lambda1&lt;System.Func`2[Pathoschild.WebApi.NhibernateOdata.Tests.Models.Parent,System.Boolean]&gt;(Pathoschild.WebApi.NhibernateOdata.Tests.Models.Parent $$it)
	///    {
	///        (.If ($$it.Component == null) {
	///            null
	///        } .Else {
	///            (System.Nullable`1[System.Int32])($$it.Component).Two
	///        } == (System.Nullable`1[System.Int32]).Constant&lt;System.Web.Http.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]&gt;(System.Web.Http.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]).TypedProperty)
	///        == True
	///    }
	/// </code>
	/// </remarks>
	public class FixComponentNullCheckVisitor : ExpressionVisitor
	{
		/*********
		** Properties
		*********/
		/// <summary>Whether the visitor is visiting a nested node.</summary>
		/// <remarks>This is used to recognize the top-level node for logging.</remarks>
		private bool IsRecursing;

		/// <summary>Dispatches the expression to one of the more specialized visit methods in this class.</summary>
		/// <param name="node">The expression to visit.</param>
		/// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
		public override Expression Visit(Expression node)
		{
			// top node
			if (!this.IsRecursing)
			{
				this.IsRecursing = true;
				return base.Visit(node);
			}

			var conditionalExpression = node as ConditionalExpression;
			if (conditionalExpression != null)
				return this.HandleConditionalExpression(node, conditionalExpression);

			return base.Visit(node);
		}

		/*********
		** Protected methods
		*********/
		/// <summary>Handles the conditional expression (equivalent to <c>.If {} .Else {}</c> in the sample expression tree in the <see cref="FixStringMethodsVisitor"/> remarks).</summary>
		/// <param name="original">The original expression.</param>
		/// <param name="ifElse">The conditional expression.</param>
		/// <returns>A reduced if/else statement if it contains any of the matched methods. Otherwise, the original expression.</returns>
		private Expression HandleConditionalExpression(Expression original, ConditionalExpression ifElse)
		{
			var binaryExpression = ifElse.Test as BinaryExpression;
			if (binaryExpression != null && binaryExpression.Right is ConstantExpression)
			{
				if (((ConstantExpression)binaryExpression.Right).Value == null)
				{
					// Ignore the null check and always return the value.
					return base.Visit(ifElse.IfFalse);
				}
			}

			return base.Visit(original);
		}
	}
}
